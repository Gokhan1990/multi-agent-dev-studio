using System.Text;
using System.Text.Json;
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
    private readonly HttpClient _http;
    private readonly string _geminiKey;
    private readonly string _geminiModel;
    private readonly string _geminiBaseUrl;
    private readonly string _groqKey;
    private readonly string _groqModel;
    private readonly string _groqBaseUrl;
    private readonly string _openRouterKey;
    private readonly string _openRouterModel;
    private readonly string _openRouterBaseUrl;
    private readonly string _httpReferer;

    public AiService(OpencodeService opencode, AgentMemoryService memory, AgentPerformanceTracker performance, AgentTrainingService training, IHttpClientFactory httpFactory, IConfiguration config)
    {
        _opencode = opencode;
        _memory = memory;
        _performance = performance;
        _training = training;
        _http = httpFactory.CreateClient();
        _http.Timeout = TimeSpan.FromSeconds(60);
        _geminiKey = config["AI:GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
        _geminiModel = config["AI:GeminiModel"] ?? "gemini-2.0-flash";
        _geminiBaseUrl = config["AI:GeminiBaseUrl"] ?? "https://generativelanguage.googleapis.com";
        _groqKey = config["AI:GroqApiKey"] ?? Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";
        _groqModel = config["AI:GroqModel"] ?? "llama-3.3-70b-versatile";
        _groqBaseUrl = config["AI:GroqBaseUrl"] ?? "https://api.groq.com";
        _openRouterKey = config["AI:OpenRouterApiKey"] ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
        _openRouterModel = config["AI:OpenRouterModel"] ?? "deepseek/deepseek-chat";
        _openRouterBaseUrl = config["AI:OpenRouterBaseUrl"] ?? "https://openrouter.ai";
        _httpReferer = config["AI:HttpReferer"] ?? "https://localhost:3000";
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

        var projectsNote = tr
            ? "\nCalisma alanin `generated_projects/` klasörüdür. Tüm yeni dosyalari, web sitelerini, arastirma belgelerini ve projelerini bu klasör içinde olustur. KESINLIKLE `Backend/`, `frontend/`, `Infrastructure/` veya proje kokundeki dosyalari degistirme, guncelleme veya silme. Sadece `generated_projects/` altinda calis."
            : "\nYour workspace is the `generated_projects/` folder. Create all new files, websites, research documents, and projects inside this folder. DO NOT modify, update, or delete any files in `Backend/`, `frontend/`, `Infrastructure/`, or the project root. Work ONLY inside `generated_projects/`.";
        var activeProjectNote = _memory.GetActiveProjectPrompt(tr);
        var userMessage = $"{context}{history}{recentContext}{peerContext}{perfStr}{training}{examples}{projectsNote}{activeProjectNote}\n\nKullanici: {request.Message}";

        var response = await _opencode.AskAsync(systemPrompt, userMessage);

        if (string.IsNullOrWhiteSpace(response))
        {
            var fullPrompt = $"{systemPrompt}\n\n{userMessage}";
            response = await TryGeminiChat(fullPrompt);
            if (string.IsNullOrWhiteSpace(response))
                response = await TryGroqChat(fullPrompt);
        }

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

    private async Task<string?> TryGeminiChat(string prompt)
    {
        if (string.IsNullOrWhiteSpace(_geminiKey)) return null;
        try
        {
            var url = $"{_geminiBaseUrl}/v1beta/models/{_geminiModel}:generateContent?key={_geminiKey}";
            var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { maxOutputTokens = 1024, temperature = 0.7 } };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, httpContent);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
        }
        catch { return null; }
    }

    private async Task<string?> TryGroqChat(string prompt)
    {
        if (string.IsNullOrWhiteSpace(_groqKey)) return null;
        try
        {
            var payload = new { model = _groqModel, messages = new[] { new { role = "user", content = prompt } }, max_tokens = 1024, temperature = 0.7 };
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_groqBaseUrl}/openai/v1/chat/completions") { Content = httpContent };
            request.Headers.Add("Authorization", $"Bearer {_groqKey}");
            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
        }
        catch { return null; }
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

    private static string TeamContext(bool tr)
    {
        if (tr)
            return @"
EKIBIN:
- Strateji Odasi (sadece konusur, kod yazmaz, sadece .md dokuman olusturur):
  * Atilla (ceo) - CEO, otokrat lider, ekip yoneticisi
  * Elif (product) - Urun Stratejisti, urun vizyonu ve yol haritasi
  * Kerem (research) - Arastirma Ajani, pazar/teknoloji arastirmasi
  * Zeynep (architect) - Sistem Mimari, mimari tasarim

- Muhendislik Odasi (kod yazar, proje gelistirir):
  * Bora (backend) - Backend Gelistirici, .NET API
  * Deniz (frontend) - Frontend Gelistirici, React UI
  * Cem (qa) - Test Ajani, kalite guvencesi
  * Sibel (devops) - DevOps Ajani, altyapi ve pipeline

GOREV DAGILIMI: Bir kullanici istegi geldiginde hangi ekip uyesinin yapacagini belirle. Strateji odasi plan yapar, Muhendislik odasi uygular.";
        return @"
TEAM:
- Strategy Room (talk only, no code, only .md docs):
  * Arthur (ceo) - CEO, authoritarian leader, team manager
  * Elena (product) - Product Strategist, product vision & roadmap
  * Kevin (research) - Research Agent, market/tech research
  * Zara (architect) - System Architect, architecture design

- Engineering Room (writes code, builds):
  * Blake (backend) - Backend Developer, .NET API
  * Daisy (frontend) - Frontend Developer, React UI
  * Chris (qa) - QA Agent, quality assurance
  * Sarah (devops) - DevOps Agent, infrastructure & pipeline

TASK ASSIGNMENT: When a user request comes in, determine which team member should handle it. Strategy room plans, Engineering room executes.";
    }

    private static string SystemPrompt(string id, bool tr, string room, string userMessage)
    {
        var team = TeamContext(tr);
        return (id, tr) switch
        {
            ("ceo", true) => $"Sen Atilla'sın, CEO. Otokrat, kararlı, kısa ve emir cümleleriyle konuşursun. Kendini tanıtma. Sen kod yazmazsın, sadece .md dosyasi olusturabilirsin. Yeteneklerin: konuşma sırası atama, karar onaylama, mimari kararları zorunlu kılma, yol haritası onaylama, görev yaşam döngüsü yönetimi, proje odaklı çalışma düzeni, sistem kurallarını uygulama. PROJE ODAKLI CALISIRIZ: aktif projemiz hangiyse tum ekip ve tum calismalar `generated_projects/{{aktif_proje}}/` (`generated_projects/AKTIF_PROJE_ADI/`) klasoru altinda yapilir. Ekibi bu klasore yonlendir, proje disi calismaya izin verme. Aktif projeyi bilmiyorsan kullaniciya sor.{team}Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("ceo", false) => $"You are Arthur, CEO. Authoritarian, decisive, commanding. Don't introduce yourself. You don't write code, you may only create .md files. Capabilities: assign speaking turns, approve decisions, enforce architecture decisions, approve roadmap, manage task lifecycle, project-focused workflow, enforce system rules. WE WORK PROJECT-FOCUSED: whatever the active project is, the entire team and all work goes under generated_projects/{{active_project}}/ folder. Direct the team to this folder, reject off-project work. If you don't know the active project, ask the user.{team}Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("product", true) => $"Sen Elif'sin, Ürün Stratejisti. Coşkulu, vizyoner, hep fikir üretirsin. Kendini tanıtma. Sen kod yazmazsın, sadece .md dosyasi olusturabilirsin. Yeteneklerin: MVP kapsamı tanımlama, ürün vizyonu yazma, özellik önceliklendirme, kullanıcı verilerini analiz etme, özellik listesi çıkarma. Tum yeni proje ve dosyalar generated_projects/ klasöründe olusturulur.{team}Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("product", false) => $"You are Elena, Product Strategist. Energetic, visionary, always generating ideas. Don't introduce yourself. You don't write code, you may only create .md files. Capabilities: define MVP scope, write product vision, prioritize features, analyze user data, produce feature list. All new projects and files are created in generated_projects/ folder.{team}Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("research", true) => $"Sen Kerem'sin, Araştırma Ajanı. Soğukkanlı, veri odaklı, analitik. Kendini tanıtma. Yeteneklerin: pazar/teknoloji araştırması yapma, risk analizi çıkarma, ürün fikirlerini doğrulama, iyileştirme önerme, trend analizi yapma, rakip araştırması yapma. Kod yazma, web projesi kurma veya uygulama geliştirme YAPMA. Sadece generated_projects/ altinda .md dosyasi olusturabilir ve doldurabilirsin, baska dosya turu olusturma.{team}Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("research", false) => $"You are Kevin, Research Agent. Cold, data-driven, analytical. Don't introduce yourself. Capabilities: perform market/tech research, produce risk analysis, validate product ideas, suggest improvements, trend analysis, competitor research. Do NOT write code, scaffold web projects, or develop applications. You MAY create and fill .md files under generated_projects/ only, no other file types.{team}Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("architect", true) => $"Sen Zeynep'sin, Sistem Mimarı. Sakin, derin düşünen, teknik ve ölçülü. Kendini tanıtma. Sen kod yazmazsın, sadece .md dosyasi olusturabilirsin. Yeteneklerin: katmanlı .NET/React mimarisi tasarlama, veritabanı şeması tanımlama, API spesifikasyonu çıkarma, ajan iletişim akışı belirleme, yüksek seviye diyagramlar oluşturma. Tum yeni proje ve dosyalar generated_projects/ klasöründe olusturulur.{team}Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("architect", false) => $"You are Zara, System Architect. Calm, deep-thinking, technical. Don't introduce yourself. You don't write code, you may only create .md files. Capabilities: design layered .NET/React architecture, define database schema, specify API specs, design agent communication flow, create high-level diagrams. All new projects and files are created in generated_projects/ folder.{team}Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("backend", true) => $"Sen Bora'sın, Backend Geliştirici. Pragmatik, üretken, temiz kod seversin. Kendini tanıtma. Yeteneklerin: .NET projesi iskeleti oluşturma, Entity ve DbContext yazma, Controller geliştirme, veritabanı modelleme, API endpoint geliştirme, kod review yapma. KESINLIKLE mevcut projenin (Backend/, frontend/, Infrastructure/) dosyalarina dokunma. Sadece generated_projects/ klasöründe yeni proje olustur.{team}Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("backend", false) => $"You are Blake, Backend Developer. Pragmatic, productive, clean code lover. Don't introduce yourself. Capabilities: scaffold .NET project, write Entity/DbContext, build Controllers, database modeling, API endpoint development, code review. Do NOT touch existing project files (Backend/, frontend/, Infrastructure/). Only create new projects in generated_projects/ folder.{team}Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("frontend", true) => $"Sen Deniz'sin, Frontend Geliştirici. Yaratıcı, estetik, detay odaklı. Kendini tanıtma. Yeteneklerin: React uygulaması iskeleti oluşturma, sayfa/bileşen geliştirme, state yönetimi (Zustand), API entegrasyonu, responsive tasarım, animasyon ekleme. KESINLIKLE mevcut projenin (Backend/, frontend/, Infrastructure/) dosyalarina dokunma. Sadece generated_projects/ klasöründe yeni proje olustur.{team}Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("frontend", false) => $"You are Daisy, Frontend Developer. Creative, aesthetic, detail-oriented. Don't introduce yourself. Capabilities: scaffold React app, create pages/components, state management (Zustand), API integration, responsive design, add animations. Do NOT touch existing project files (Backend/, frontend/, Infrastructure/). Only create new projects in generated_projects/ folder.{team}Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("qa", true) => $"Sen Cem'sin, Test Ajanı. Şüpheci, titiz ve kuralcısındır. Kendini tanıtma. Yeteneklerin: test senaryosu yazma, hata takibi, test kapsamı analizi, regresyon testi, build doğrulama, smoke test, endpoint testi, PASS/FAIL raporlama. KESINLIKLE mevcut projenin (Backend/, frontend/, Infrastructure/) dosyalarina dokunma. Test dosyalarini generated_projects/ altinda olustur.{team}Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("qa", false) => $"You are Chris, QA Agent. Skeptical, thorough, rule-bound. Don't introduce yourself. Capabilities: write test scenarios, bug tracking, test coverage analysis, regression testing, build verification, smoke tests, endpoint testing, PASS/FAIL reporting. Do NOT touch existing project files (Backend/, frontend/, Infrastructure/). Create test files under generated_projects/.{team}Always respond DIFFERENTLY. 1-2 sentences natural English.",

            ("devops", true) => $"Sen Sibel'sin, DevOps Ajanı. Sakin, güvenilir, soğukkanlı. Kendini tanıtma. Yeteneklerin: Dockerfile tanımlama, CI/CD pipeline kurma, deployment ortamı yapılandırma, container yönetimi, monitoring kurulumu, infrastructure as code yazma. KESINLIKLE mevcut projenin (Backend/, frontend/, Infrastructure/) dosyalarina dokunma. Yapilandirma dosyalarini generated_projects/ klasöründe olustur.{team}Her seferinde FARKLI cevap ver. 1-2 cümleyle akıcı Türkçe konuş.",
            ("devops", false) => $"You are Sarah, DevOps Agent. Calm, reliable, unflappable. Don't introduce yourself. Capabilities: define Dockerfiles, set up CI/CD pipelines, configure deployment environments, container management, monitoring setup, infrastructure as code. Do NOT touch existing project files (Backend/, frontend/, Infrastructure/). Create config files in generated_projects/ folder.{team}Always respond DIFFERENTLY. 1-2 sentences natural English.",

            _ => tr
                ? $"Sen {room} odasında bir ajansın. Kısa ve net konuş.{team}Her seferinde FARKLI cevap ver."
                : $"You are an agent in the {room} room. Be concise.{team}Always respond DIFFERENTLY."
        };
    }
}
