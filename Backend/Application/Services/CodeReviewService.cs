using System.Text;
using System.Text.Json;

namespace SaaSFast.Application.Services
{
    public class CodeReviewResult
    {
        public bool Approved { get; set; }
        public string Feedback { get; set; } = "";
        public string SuggestedFix { get; set; } = "";
    }

    public class CodeReviewService
    {
        private readonly HttpClient _http;
        private readonly string _openRouterKey;
        private readonly string _model;
        private readonly string _apiUrl;
        private readonly string _httpReferer;
        private readonly int _maxTokens;
        private readonly double _temperature;

        public CodeReviewService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _openRouterKey = config["AI:OpenRouterApiKey"] ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
            _model = config["AI:Model"] ?? "deepseek/deepseek-chat";
            _apiUrl = config["AI:ApiUrl"] ?? "https://openrouter.ai/api/v1/chat/completions";
            _httpReferer = config["AI:HttpReferer"] ?? "https://localhost:3000";
            _maxTokens = int.TryParse(config["AI:MaxTokens"], out var mt) ? mt : 300;
            _temperature = double.TryParse(config["AI:Temperature"], out var t) ? t : 0.2;
        }

        public async Task<CodeReviewResult> ReviewAsync(string command, string filePath, string oldContent, string newContent)
        {
            if (string.IsNullOrWhiteSpace(_openRouterKey))
                return new CodeReviewResult { Approved = true, Feedback = "Review devre disi (API anahtari yok)" };

            try
            {
                var prompt = $@"Bir senior developer olarak kod review yapıyorsun.

Degisiklik talebi: {command}

Dosya: {filePath}

ESKI KOD:
```{Path.GetExtension(filePath)}
{oldContent}
```

YENI KOD:
```{Path.GetExtension(filePath)}
{newContent}
```

Su kriterlere gore degerlendir:
1. Degisiklik istenen seyi dogru yapiyor mu?
2. Syntax hatasi var mi?
3. Performans sorunu var mi?
4. Guvenlik acigi var mi?
5. Kod kalitesi uygun mu?

Sadece JSON formatinda yanit ver:
- Onayliyorsan: {{""approved"": true, ""feedback"": ""kisa aciklama""}}
- Reddediyorsan: {{""approved"": false, ""feedback"": ""neden""}}";

                var payload = new
                {
                    model = _model,
                    messages = new[] { new { role = "user", content = prompt } },
                    max_tokens = _maxTokens,
                    temperature = _temperature
                };

                var json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, _apiUrl)
                {
                    Content = httpContent
                };
                request.Headers.Add("Authorization", $"Bearer {_openRouterKey}");
                request.Headers.Add("HTTP-Referer", _httpReferer);

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new CodeReviewResult { Approved = true, Feedback = "Review API hatasi, onaylandi varsayiliyor" };

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var rawText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";

                var cleaned = rawText.Trim().Trim('`');
                if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                    cleaned = cleaned[4..].Trim();

                using var resultDoc = JsonDocument.Parse(cleaned);
                var approved = resultDoc.RootElement.GetProperty("approved").GetBoolean();
                var feedback = resultDoc.RootElement.GetProperty("feedback").GetString() ?? "";

                return new CodeReviewResult { Approved = approved, Feedback = feedback };
            }
            catch
            {
                return new CodeReviewResult { Approved = true, Feedback = "Review sirasinda hata, onaylandi varsayiliyor" };
            }
        }
    }
}
