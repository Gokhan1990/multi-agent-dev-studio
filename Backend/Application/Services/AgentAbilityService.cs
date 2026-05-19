namespace SaaSFast.Application.Services;

public class AgentAbility
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string AgentId { get; set; } = "";
}

public class AgentAbilityService
{
    public List<AgentAbility> GetAll()
    {
        return new List<AgentAbility>
        {
            // CEO - Atilla/Arthur
            new() { Id = "ceo-turn", Name = "Konuşma Sırası Atama", Description = "Ekip üyelerine söz hakkı verir, toplantı akışını düzenler", AgentId = "ceo" },
            new() { Id = "ceo-approve", Name = "Karar Onaylama", Description = "Stratejik kararları onaylar veya reddeder", AgentId = "ceo" },
            new() { Id = "ceo-enforce", Name = "Mimari Kararları Zorunlu Kılma", Description = "Teknik kararların uygulanmasını sağlar", AgentId = "ceo" },
            new() { Id = "ceo-roadmap", Name = "Yol Haritası Onaylama", Description = "Ürün yol haritasını onaylar", AgentId = "ceo" },
            new() { Id = "ceo-tasklifecycle", Name = "Görev Yaşam Döngüsü Yönetimi", Description = "Görevlerin planlanması, atanması ve tamamlanmasını takip eder", AgentId = "ceo" },
            new() { Id = "ceo-enforcerules", Name = "Sistem Kurallarını Uygulama", Description = "Takım kurallarına uyulmasını sağlar", AgentId = "ceo" },

            // Product - Elif/Elena
            new() { Id = "product-mvp", Name = "MVP Kapsamı Tanımlama", Description = "Minimum viable product sınırlarını belirler", AgentId = "product" },
            new() { Id = "product-vision", Name = "Ürün Vizyonu Yazma", Description = "Ürün vizyonu ve stratejisini dokümante eder", AgentId = "product" },
            new() { Id = "product-prioritize", Name = "Özellik Önceliklendirme", Description = "Kullanıcı değerine göre özellikleri sıralar", AgentId = "product" },
            new() { Id = "product-analyze", Name = "Kullanıcı Verilerini Analiz Etme", Description = "Kullanıcı araştırması verilerini yorumlar", AgentId = "product" },
            new() { Id = "product-featurelist", Name = "Özellik Listesi Çıkarma", Description = "Gereksinimleri maddeler halinde yapılandırır", AgentId = "product" },

            // Research - Kerem/Kevin
            new() { Id = "research-market", Name = "Pazar Araştırması Yapma", Description = "Hedef pazarı analiz eder, büyüklük ve trendleri belirler", AgentId = "research" },
            new() { Id = "research-tech", Name = "Teknoloji Araştırması Yapma", Description = "Yeni teknolojileri değerlendirir, uygunluk analizi yapar", AgentId = "research" },
            new() { Id = "research-risk", Name = "Risk Analizi Çıkarma", Description = "Olası riskleri tanımlar ve etki analizi yapar", AgentId = "research" },
            new() { Id = "research-validate", Name = "Ürün Fikirlerini Doğrulama", Description = "Fikirlerin fizibilitesini ve pazar uyumunu test eder", AgentId = "research" },
            new() { Id = "research-improve", Name = "İyileştirme Önerme", Description = "Veriye dayalı iyileştirme önerileri sunar", AgentId = "research" },
            new() { Id = "research-trend", Name = "Trend Analizi", Description = "Sektör trendlerini takip eder ve raporlar", AgentId = "research" },

            // Architect - Zeynep/Zara
            new() { Id = "architect-layered", Name = "Katmanlı Mimari Tasarlama", Description = ".NET/React uygulamaları için katmanlı mimari kurgular", AgentId = "architect" },
            new() { Id = "architect-schema", Name = "Veritabanı Şeması Tanımlama", Description = "Entity ilişkilerini ve veritabanı yapısını belirler", AgentId = "architect" },
            new() { Id = "architect-api", Name = "API Spesifikasyonu Çıkarma", Description = "REST/GraphQL API kontratlarını dokümante eder", AgentId = "architect" },
            new() { Id = "architect-flow", Name = "Ajan İletişim Akışı Belirleme", Description = "Servisler arası iletişim protokollerini tasarlar", AgentId = "architect" },
            new() { Id = "architect-diagram", Name = "Yüksek Seviye Diyagramlar Oluşturma", Description = "Sistem bileşenlerini görselleştirir", AgentId = "architect" },

            // Backend - Bora/Blake
            new() { Id = "backend-scaffold", Name = ".NET Projesi İskeleti Oluşturma", Description = "Sıfırdan .NET projesi kurar, katmanları yapılandırır", AgentId = "backend" },
            new() { Id = "backend-entity", Name = "Entity ve DbContext Yazma", Description = "Veritabanı modellerini ve DbContext'i kodlar", AgentId = "backend" },
            new() { Id = "backend-controller", Name = "Controller Geliştirme", Description = "REST API controller'larını yazar", AgentId = "backend" },
            new() { Id = "backend-db", Name = "Veritabanı Modelleme", Description = "Tablo yapılarını ve ilişkileri tasarlar", AgentId = "backend" },
            new() { Id = "backend-api", Name = "API Endpoint Geliştirme", Description = "CRUD ve iş mantığı endpoint'lerini implemente eder", AgentId = "backend" },
            new() { Id = "backend-review", Name = "Kod Review Yapma", Description = "Backend kodunu inceler, iyileştirme önerir", AgentId = "backend" },

            // Frontend - Deniz/Daisy
            new() { Id = "frontend-scaffold", Name = "React Uygulaması İskeleti Oluşturma", Description = "Vite/React projesi kurar, dosya yapısını oluşturur", AgentId = "frontend" },
            new() { Id = "frontend-component", Name = "Sayfa/Bileşen Geliştirme", Description = "Kullanıcı arayüzü bileşenleri ve sayfaları kodlar", AgentId = "frontend" },
            new() { Id = "frontend-state", Name = "State Yönetimi (Zustand)", Description = "Zustand store'ları ile uygulama durumunu yönetir", AgentId = "frontend" },
            new() { Id = "frontend-api", Name = "API Entegrasyonu", Description = "Backend endpoint'lerine bağlanır, veri çeker/gönderir", AgentId = "frontend" },
            new() { Id = "frontend-responsive", Name = "Responsive Tasarım", Description = "Mobil ve masaüstü uyumlu arayüzler geliştirir", AgentId = "frontend" },
            new() { Id = "frontend-animation", Name = "Animasyon Ekleme", Description = "CSS ve JS animasyonları ile kullanıcı deneyimini iyileştirir", AgentId = "frontend" },

            // QA - Cem/Chris
            new() { Id = "qa-scenario", Name = "Test Senaryosu Yazma", Description = "Kapsamlı test senaryoları ve test case'leri hazırlar", AgentId = "qa" },
            new() { Id = "qa-bugtrack", Name = "Hata Takibi", Description = "Bulunan hataları kaydeder, önceliklendirir ve raporlar", AgentId = "qa" },
            new() { Id = "qa-coverage", Name = "Test Kapsamı Analizi", Description = "Kodun hangi bölümlerinin test edildiğini analiz eder", AgentId = "qa" },
            new() { Id = "qa-regression", Name = "Regresyon Testi", Description = "Yeni değişikliklerin mevcut işlevselliği bozmadığını doğrular", AgentId = "qa" },
            new() { Id = "qa-build", Name = "Build Doğrulama", Description = "Derleme sonrası temel kontrolleri yapar", AgentId = "qa" },
            new() { Id = "qa-smoke", Name = "Smoke Test", Description = "Kritik yol senaryolarını hızlıca test eder", AgentId = "qa" },
            new() { Id = "qa-endpoint", Name = "Endpoint Testi", Description = "API uç noktalarını test eder", AgentId = "qa" },
            new() { Id = "qa-report", Name = "PASS/FAIL Raporlama", Description = "Test sonuçlarını raporlar", AgentId = "qa" },

            // DevOps - Sibel/Sarah
            new() { Id = "devops-dockerfile", Name = "Dockerfile Tanımlama", Description = "Uygulamalar için optimize Docker image'ları hazırlar", AgentId = "devops" },
            new() { Id = "devops-cicd", Name = "CI/CD Pipeline Kurma", Description = "Otomatik derleme, test ve dağıtım süreçleri oluşturur", AgentId = "devops" },
            new() { Id = "devops-deploy", Name = "Deployment Ortamı Yapılandırma", Description = "Geliştirme/staging/production ortamlarını kurar", AgentId = "devops" },
            new() { Id = "devops-container", Name = "Container Yönetimi", Description = "Docker container'larını yönetir, orchestration sağlar", AgentId = "devops" },
            new() { Id = "devops-monitor", Name = "Monitoring Kurulumu", Description = "Uygulama ve altyapı izleme sistemleri kurar", AgentId = "devops" },
            new() { Id = "devops-iac", Name = "Infrastructure as Code", Description = "Terraform vb. araçlarla altyapıyı kod olarak yazar", AgentId = "devops" },
        };
    }

    public List<AgentAbility> GetByAgent(string agentId)
    {
        return GetAll().Where(a => a.AgentId == agentId).ToList();
    }
}
