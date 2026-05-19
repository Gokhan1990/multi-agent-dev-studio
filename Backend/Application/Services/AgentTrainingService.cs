using System.Text;

namespace SaaSFast.Application.Services;

public class AgentTrainingService
{
    private readonly AgentAbilityService _abilities;

    public AgentTrainingService(AgentAbilityService abilities)
    {
        _abilities = abilities;
    }

    public string GetTrainingContext(string agentId, bool tr)
    {
        var agentAbilities = _abilities.GetByAgent(agentId);
        if (agentAbilities.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine(tr ? "\n=== YETENEK EĞİTİMİ ===" : "\n=== ABILITY TRAINING ===");

        foreach (var ability in agentAbilities)
        {
            sb.AppendLine(tr
                ? $"• {ability.Name}: {ability.Description}"
                : $"• {ability.Name}: {ability.Description}");
        }

        sb.AppendLine(tr
            ? "\nKullanım: Bir yeteneğini kullanmak istediğinde doğal dilde ifade et. Örn: 'API'yi test ediyorum' veya 'Pazar araştırmasını tamamladım'."
            : "\nUsage: Express your ability usage in natural language. E.g.: 'Testing the API' or 'Market research completed'.");

        return sb.ToString();
    }

    public string GetAbilityExamplePrompt(string agentId, bool tr)
    {
        return (agentId, tr) switch
        {
            ("ceo", true) => "\nÖrnek: 'Ekibi topluyorum, yol haritasını onaylayalım.' — 'Sprint hedeflerini belirledim, herkes görevini biliyor.'",
            ("ceo", false) => "\nExample: 'Gathering the team, let's approve the roadmap.' — 'Sprint goals set, everyone knows their task.'",

            ("product", true) => "\nÖrnek: 'MVP kapsamını belirledim, ilk sürüme 3 özellik giriyor.' — 'Kullanıcı geri bildirimlerini analiz ettim, öncelik sırası hazır.'",
            ("product", false) => "\nExample: 'MVP scope defined, 3 features for the first release.' — 'User feedback analyzed, priority list ready.'",

            ("research", true) => "\nÖrnek: 'Pazar araştırması tamam, rakiplerin güçlü yönlerini analiz ettim.' — 'Risk değerlendirmesi yaptım, 2 kritik risk tespit edildi.'",
            ("research", false) => "\nExample: 'Market research done, analyzed competitor strengths.' — 'Risk assessment complete, 2 critical risks identified.'",

            ("architect", true) => "\nÖrnek: 'Mimariyi gözden geçirdim, katmanlı yapı uygun.' — 'Veritabanı şemasını çıkardım, entity ilişkileri net.'",
            ("architect", false) => "\nExample: 'Architecture reviewed, layered structure is suitable.' — 'Database schema designed, entity relationships clear.'",

            ("backend", true) => "\nÖrnek: 'API endpoint'lerini yazdım, controller ve service katmanı hazır.' — 'Veritabanı modelini kurdum, migration çalışıyor.'",
            ("backend", false) => "\nExample: 'API endpoints written, controller and service layer ready.' — 'Database model set up, migration running.'",

            ("frontend", true) => "\nÖrnek: 'Bileşeni oluşturdum, responsive tasarım uygulandı.' — 'API entegrasyonu tamam, state yönetimi Zustand ile kuruldu.'",
            ("frontend", false) => "\nExample: 'Component created, responsive design applied.' — 'API integration done, state management set up with Zustand.'",

            ("qa", true) => "\nÖrnek: 'Test senaryolarını yazdım, smoke test geçti.' — 'Regresyon testi tamam, yeni hata bulunamadı.'",
            ("qa", false) => "\nExample: 'Test scenarios written, smoke test passed.' — 'Regression testing complete, no new bugs found.'",

            ("devops", true) => "\nÖrnek: 'Dockerfile'ı optimize ettim, image boyutu küçüldü.' — 'CI/CD pipeline kuruldu, deploy otomatik.'",
            ("devops", false) => "\nExample: 'Dockerfile optimized, image size reduced.' — 'CI/CD pipeline set up, deployment automated.'",

            _ => ""
        };
    }
}
