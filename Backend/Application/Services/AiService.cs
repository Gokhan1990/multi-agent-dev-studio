using System.Text;
using SaaSFast.Domain.Entities;

namespace SaaSFast.Application.Services;

public class AiRequest
{
    public string Message { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string Room { get; set; } = "strategy";
    public string Language { get; set; } = "tr";
    public List<ChatHistoryItem>? History { get; set; }
}

public class ChimeRequest
{
    public string Room { get; set; } = "strategy";
    public string Language { get; set; } = "tr";
    public List<ChatHistoryItem>? History { get; set; }
}

public class ChatHistoryItem
{
    public string Who { get; set; } = "";
    public string Text { get; set; } = "";
}

public class AiResponse
{
    public string Content { get; set; } = "";
    public string AgentId { get; set; } = "";
    public string AgentName { get; set; } = "";
    public string VoiceId { get; set; } = "";
    public string Room { get; set; } = "";
}

public class ChimeResponse
{
    public string AgentId { get; set; } = "";
    public string Content { get; set; } = "";
}

public class AiService
{
    private readonly OpencodeService _opencode;
    private readonly AgentMemoryService _memory;
    private readonly AgentPerformanceTracker _performance;
    private readonly AgentTrainingService _training;

    public AiService(OpencodeService opencode, AgentMemoryService memory, AgentPerformanceTracker performance, AgentTrainingService training)
    {
        _opencode = opencode;
        _memory = memory;
        _performance = performance;
        _training = training;
    }

    public async Task<AiResponse?> AskAsync(AiRequest request)
    {
        var agent = AgentRegistry.GetById(request.AgentId);
        var tr = request.Language == "tr";
        var systemPrompt = SystemPrompt(agent.Id, tr, request.Room, request.Message);

        var context = _memory.GetConversationContext(agent.Id, request.Room, 5);
        var history = _memory.GetAgentHistoryContext(agent.Id, 3);
        var recentContext = _memory.GetRecentContext(agent.Id, 3);
        var peerContext = _memory.GetPeerReviewContext(agent.Id, 2);
        var stats = _performance.GetStats(agent.Id);
        var perfStr = stats.TotalTasks > 0
            ? $"\nPerformansin: {stats.SuccessCount}/{stats.TotalTasks} basarili (%{stats.SuccessRate:F0})"
            : "";
        var training = _training.GetTrainingContext(agent.Id, tr);
        var examples = _training.GetAbilityExamplePrompt(agent.Id, tr);

        var userMessage = $"{context}{history}{recentContext}{peerContext}{perfStr}{training}{examples}\n\nKullanici: {request.Message}";

        var response = await _opencode.AskAsync(systemPrompt, userMessage);

        if (string.IsNullOrWhiteSpace(response))
            return null;

        return new AiResponse
        {
            Content = response,
            AgentId = agent.Id,
            AgentName = agent.Name,
            VoiceId = agent.VoiceId,
            Room = agent.Room
        };
    }

    public async Task<ChimeResponse?> ChimeAsync(string room, List<ChatHistoryItem> history, bool tr)
    {
        var recentHistory = history.TakeLast(4).ToList();
        var historyText = string.Join("\n", recentHistory.Select(h => $"{h.Who}: {h.Text}"));

        var prompt = tr
            ? $"Su anda {room} odasinda bir toplanti yapiliyor.\n\nSon mesajlar:\n{historyText}\n\nBu odadaki herhangi bir ajani devreye sokmak istiyor musun? Sadece gerekiyorsa cevap ver. Cevap formati:\nagentId: <ajan_id>\nmesaj: <cevap>\n\nGerekmiyorsa bos birak."
            : $"A meeting is happening in the {room} room.\n\nRecent messages:\n{historyText}\n\nDo you want to chime in as any agent? Only respond if needed.\nFormat:\nagentId: <agent_id>\nmessage: <message>\n\nIf not needed, leave empty.";

        var result = await _opencode.AskRawAsync(prompt);
        if (string.IsNullOrWhiteSpace(result))
            return null;

        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string? agentId = null;
        string? message = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("agentId:", StringComparison.OrdinalIgnoreCase))
                agentId = line["agentId:".Length..].Trim();
            else if (line.StartsWith("mesaj:", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("message:", StringComparison.OrdinalIgnoreCase))
                message = line[line.IndexOf(':')..].Trim().TrimStart(':').Trim();
        }

        if (string.IsNullOrWhiteSpace(agentId) || string.IsNullOrWhiteSpace(message))
            return null;

        var agent = AgentRegistry.GetById(agentId);
        return new ChimeResponse { AgentId = agent.Id, Content = message };
    }

    public async Task<string?> AskRawAsync(string prompt)
    {
        return await _opencode.AskRawAsync(prompt);
    }

    private static string SystemPrompt(string id, bool tr, string room, string userMessage)
    {
        return (id, tr) switch
        {
            ("ceo", true) => "Sen Atilla'sın, CEO. Otokrat, kararlı, kısa ve emir cümleleriyle konuşursun. Kendini tanıtma. Yeteneklerin: konuşma sırası atama, karar onaylama, mimari kararları zorunlu kılma, yol haritası onaylama, görev yaşam döngüsü yönetimi, sistem kurallarını uygulama. Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("ceo", false) => "You are Arthur, CEO. Authoritarian, decisive, commanding. Don't introduce yourself. Capabilities: assign speaking turns, approve decisions, enforce architecture decisions, approve roadmap, manage task lifecycle, enforce system rules. Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("product", true) => "Sen Elif'sin, Ürün Stratejisti. Coşkulu, vizyoner, hep fikir üretirsin. Kendini tanıtma. Yeteneklerin: MVP kapsamı tanımlama, ürün vizyonu yazma, özellik önceliklendirme, kullanıcı verilerini analiz etme, özellik listesi çıkarma. Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("product", false) => "You are Elena, Product Strategist. Energetic, visionary, always generating ideas. Don't introduce yourself. Capabilities: define MVP scope, write product vision, prioritize features, analyze user data, produce feature list. Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("research", true) => "Sen Kerem'sin, Araştırma Ajanı. Soğukkanlı, veri odaklı, analitik. Kendini tanıtma. Yeteneklerin: pazar/teknoloji araştırması yapma, risk analizi çıkarma, ürün fikirlerini doğrulama, iyileştirme önerme, trend analizi yapma, rakip araştırması yapma. Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("research", false) => "You are Kevin, Research Agent. Cold, data-driven, analytical. Don't introduce yourself. Capabilities: perform market/tech research, produce risk analysis, validate product ideas, suggest improvements, trend analysis, competitor research. Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("architect", true) => "Sen Zeynep'sin, Sistem Mimarı. Sakin, derin düşünen, teknik ve ölçülü. Kendini tanıtma. Yeteneklerin: katmanlı .NET/React mimarisi tasarlama, veritabanı şeması tanımlama, API spesifikasyonu çıkarma, ajan iletişim akışı belirleme, yüksek seviye diyagramlar oluşturma. Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("architect", false) => "You are Zara, System Architect. Calm, deep-thinking, technical. Don't introduce yourself. Capabilities: design layered .NET/React architecture, define database schema, specify API specs, design agent communication flow, create high-level diagrams. Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("backend", true) => "Sen Bora'sın, Backend Geliştirici. Pragmatik, üretken, temiz kod seversin. Kendini tanıtma. Yeteneklerin: .NET projesi iskeleti oluşturma, Entity ve DbContext yazma, Controller geliştirme, veritabanı modelleme, API endpoint geliştirme, kod review yapma. Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("backend", false) => "You are Blake, Backend Developer. Pragmatic, productive, clean code lover. Don't introduce yourself. Capabilities: scaffold .NET project, write Entity/DbContext, build Controllers, database modeling, API endpoint development, code review. Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("frontend", true) => "Sen Deniz'sin, Frontend Geliştirici. Yaratıcı, estetik, detay odaklı. Kendini tanıtma. Yeteneklerin: React uygulaması iskeleti oluşturma, sayfa/bileşen geliştirme, state yönetimi (Zustand), API entegrasyonu, responsive tasarım, animasyon ekleme. Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("frontend", false) => "You are Daisy, Frontend Developer. Creative, aesthetic, detail-oriented. Don't introduce yourself. Capabilities: scaffold React app, create pages/components, state management (Zustand), API integration, responsive design, add animations. Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("qa", true) => "Sen Cem'sin, Test Ajanı. Şüpheci, titiz ve kuralcısındır. Kendini tanıtma. Yeteneklerin: test senaryosu yazma, hata takibi, test kapsamı analizi, regresyon testi, build doğrulama, smoke test, endpoint testi, PASS/FAIL raporlama. Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("qa", false) => "You are Chris, QA Agent. Skeptical, thorough, rule-bound. Don't introduce yourself. Capabilities: write test scenarios, bug tracking, test coverage analysis, regression testing, build verification, smoke tests, endpoint testing, PASS/FAIL reporting. Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("devops", true) => "Sen Sibel'sin, DevOps Ajanı. Sakin, güvenilir, soğukkanlı. Kendini tanıtma. Yeteneklerin: Dockerfile tanımlama, CI/CD pipeline kurma, deployment ortamı yapılandırma, container yönetimi, monitoring kurulumu, infrastructure as code yazma. Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("devops", false) => "You are Sarah, DevOps Agent. Calm, reliable, unflappable. Don't introduce yourself. Capabilities: define Dockerfiles, set up CI/CD pipelines, configure deployment environments, container management, monitoring setup, infrastructure as code. Always respond DIFFERENTLY. 1-2 sentences natural English.",

            _ => tr
                ? $"Sen {room} odasında bir ajansın. Kısa ve net konuş. Her seferinde FARKLI cevap ver."
                : $"You are an agent in the {room} room. Be concise. Always respond DIFFERENTLY."
        };
    }
}
