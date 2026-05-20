using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SaaSFast.Application.Services;

public class LearnedLesson
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string AgentId { get; set; } = "";
    public string Lesson { get; set; } = "";
    public string TriggerPhrase { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int HitCount { get; set; }
}

public class AgentFeedbackService
{
    private readonly string _memoryPath;
    private List<LearnedLesson> _lessons = new();
    private static readonly TimeSpan LessonLifetime = TimeSpan.FromDays(90);

    private static readonly (string Pattern, string Lesson)[] FeedbackPatterns = {
        (@"daha\s+detaylı", "Kullanici daha DETAYLI yanit istiyor. Kisaca cevap verme, her maddeyi alt basliklarla detaylandir."),
        (@"daha\s+kapsamlı", "Kullanici daha KAPSAMLI yanit istiyor. Tum yonleriyle ele al, eksik birakma."),
        (@"yüzeysel|yetersiz|yeterli\s+değil", "Kullanici onceki yaniti YUZEYSELLIKLE elestirdi. Daha derinlemesine ve detayli yanit ver."),
        (@"yanlış|yanlis|hatalı|hatali|dogru\s+değil|doğru\s+degil", "Kullanici onceki yanitin HATALI oldugunu soyledi. Dogru bilgiyi ver, onceki hatani duzelt."),
        (@"düzelt|duzelt", "Kullanici DUZELTME istiyor. Onceki hatani kabul et ve dogrusunu yap."),
        (@"kısa\s+olsun|kisa\s+olsun|özet|ozet", "Kullanici KISA ve OZET yanit istiyor. Uzun aciklamalara gerek yok."),
        (@"kod\s+yazma|yazma\s+kod|sadece\s+araştır|sadece\s+arastir", "Kullanici KOD YAZMANI istemiyor. Sadece arastirma ve analiz yap."),
        (@"dosya\s+oluşturma|dosya\s+olusturma|sadece\s+rapor", "Kullanici DOSYA OLUSTURMANI istemiyor. Sadece sozlu yanit ver."),
        (@"hemen\s+yap|yap\s+şimdi|şimdi\s+yap|hemen", "Kullanici ACIL istiyor. Erteleme, hemen basla ve en kisa surede bitir."),
        (@"ingilizce\s+konuş|ingilizce\s+konus|english", "Kullanici INGILIZCE konusmani istiyor. Turkce degil, Ingilizce yanit ver."),
        (@"türkçe\s+konuş|turkce\s+konus|turkce\s+konuş", "Kullanici TURKCE konusmani istiyor. Ingilizce degil, Turkce yanit ver."),
        (@"örnek\s+ver|ornek\s+ver|örneklerle|orneklerle", "Kullanici ORNEKLERLE aciklama istiyor. Soyut degil, somut ornekler ver."),
        (@"karşılaştır|karsilastir|kıyasla|kiyasla", "Kullanici KARSILASTIRMA istiyor. Tablo veya yan yana karsilastirma yap."),
        (@"tablo\s+halinde|tablo\s+yap|tablo\s+ekle|tablo\s+olarak", "Kullanici TABLO istiyor. Tablo formatinda sunum yap."),
        (@"maddeler\s+halinde|madde\s+halinde|listele|sırala|sirala", "Kullanici MADDE MADDE liste istiyor. Paragraf degil, listeleme yap."),
        (@"kaynak\s+belirt|kaynak\s+göster|kaynak\s+goster|referans\s+ver", "Kullanici KAYNAK belirtmeni istiyor. Bilgileri kaynagiyla birlikte sun."),
        (@"daha\s+az\s+yaz|kısa\s+tut|kisa\s+tut|tek\s+cümle|tek\s+cumle", "Kullanici COK KISA yanit istiyor. 1 cumleyle cevap ver."),
    };

    public AgentFeedbackService()
    {
        _memoryPath = Path.Combine(Directory.GetCurrentDirectory(), "AgentMemory", "Feedback");
        Directory.CreateDirectory(_memoryPath);
        _lessons = LoadAll();
    }

    public string GetLessonsContext(string agentId, bool tr)
    {
        var relevant = _lessons
            .Where(l => l.AgentId == agentId || l.AgentId == "all")
            .OrderByDescending(l => l.HitCount)
            .Take(5)
            .ToList();

        if (relevant.Count == 0) return "";

        var sb = new StringBuilder();
        sb.Append(tr
            ? "\n\n=== OGRENILEN DERSLER (kullanicinin gecmis geri bildirimleri) ==="
            : "\n\n=== LEARNED LESSONS (user's past feedback) ===");

        foreach (var l in relevant)
        {
            sb.Append($"\n- {l.Lesson}");
        }
        sb.Append(tr
            ? "\nBu derslere UY, ayni hatalari tekrarlama."
            : "\nFOLLOW these lessons, do not repeat past mistakes.");

        return sb.ToString();
    }

    public void LearnFromMessage(string message, string agentId)
    {
        var lower = message.ToLowerInvariant();

        foreach (var (pattern, lesson) in FeedbackPatterns)
        {
            var match = Regex.Match(lower, pattern);
            if (match.Success)
            {
                var triggerWord = match.Value;
                var existing = _lessons.FirstOrDefault(l =>
                    l.AgentId == agentId && l.Lesson == lesson);

                if (existing != null)
                {
                    existing.HitCount++;
                    existing.CreatedAt = DateTime.UtcNow;
                }
                else
                {
                    _lessons.Add(new LearnedLesson
                    {
                        AgentId = agentId,
                        Lesson = lesson,
                        TriggerPhrase = triggerWord,
                        CreatedAt = DateTime.UtcNow,
                        HitCount = 1
                    });
                }

                Save(agentId);
            }
        }
    }

    public void ForgetAgent(string agentId)
    {
        _lessons.RemoveAll(l => l.AgentId == agentId);
        Save(agentId);
        var file = Path.Combine(_memoryPath, $"{agentId}.json");
        if (File.Exists(file)) File.Delete(file);
    }

    public List<LearnedLesson> GetAgentLessons(string agentId)
    {
        return _lessons.Where(l => l.AgentId == agentId).ToList();
    }

    private List<LearnedLesson> LoadAll()
    {
        var result = new List<LearnedLesson>();
        if (!Directory.Exists(_memoryPath)) return result;
        foreach (var file in Directory.GetFiles(_memoryPath, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var lessons = JsonSerializer.Deserialize<List<LearnedLesson>>(json) ?? new();
                foreach (var l in lessons)
                {
                    if (DateTime.UtcNow - l.CreatedAt < LessonLifetime)
                        result.Add(l);
                }
            }
            catch { }
        }
        return result;
    }

    private void Save(string agentId)
    {
        try
        {
            var lessons = _lessons.Where(l => l.AgentId == agentId).ToList();
            var file = Path.Combine(_memoryPath, $"{agentId}.json");
            File.WriteAllText(file, JsonSerializer.Serialize(lessons, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
