using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SaaSFast.Domain.Entities;
using SaaSFast.Infrastructure.Data;

namespace SaaSFast.Application.Services
{
    public class FileDiff
    {
        public string FilePath { get; set; } = "";
        public int LinesAdded { get; set; }
        public int LinesRemoved { get; set; }
        public string DiffText { get; set; } = "";
    }

    public class CodeExecutionResult
    {
        public bool Success { get; set; }
        public string Summary { get; set; } = "";
        public string? Error { get; set; }
        public FileDiff? Diff { get; set; }
    }

    public class CodeExecutorService
    {
        private readonly string _sourceRoot;
        private readonly CommandQueueService _queue;
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
        private readonly int _maxDiffLines;
        private readonly AgentMemoryService _memory;
        private readonly AgentPerformanceTracker _performance;
        private readonly CodeReviewService _review;
        private readonly OpencodeService _opencode;
        private readonly string _generatedProjectsPath;
        private readonly AppDbContext _db;

        public CodeExecutorService(IConfiguration config, CommandQueueService queue, HttpClient http, AgentMemoryService memory, AgentPerformanceTracker performance, CodeReviewService review, OpencodeService opencode, AppDbContext db)
        {
            _sourceRoot = config.GetValue<string>("SourceRoot") ?? Directory.GetCurrentDirectory();
            _queue = queue;
            _http = http;
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
            _maxDiffLines = config.GetValue<int>("CodeExecution:MaxDiffLines", 20);
            _memory = memory;
            _performance = performance;
            _review = review;
            _opencode = opencode;
            _generatedProjectsPath = config.GetValue<string>("GeneratedProjectsPath") ?? "generated_projects";
            _db = db;
        }

        public async Task<CodeExecutionResult> ExecuteAsync(CodeCommand cmd)
        {
            var agentId = cmd.ActiveAgentId ?? "agent";

            try
            {
                if (string.IsNullOrWhiteSpace(cmd.TargetFile))
                {
                    _queue.AddLog(cmd.Id, agentId, "Hedef dosya belirtilmedi, analiz ediliyor...", "warn");
                    cmd.TargetFile = await GuessTargetFile(cmd.Text);
                    _queue.AddLog(cmd.Id, agentId, $"Hedef dosya: {cmd.TargetFile}", "info");
                }

                var normalizedTarget = cmd.TargetFile!.Replace("\\", "/");
                if (!normalizedTarget.StartsWith(_generatedProjectsPath + "/", StringComparison.OrdinalIgnoreCase) &&
                    !normalizedTarget.Equals(_generatedProjectsPath, StringComparison.OrdinalIgnoreCase))
                {
                    _queue.AddLog(cmd.Id, agentId, $"Uyari: Ajanlar sadece generated_projects/ icinde calisabilir. '{cmd.TargetFile}' engellendi, yonlendiriliyor...", "warn");
                    var activeProject = _memory.GetActiveProject();
                    var projectFolder = !string.IsNullOrWhiteSpace(activeProject) ? activeProject : "yeni-proje";
                    var fileName = Path.GetExtension(cmd.TargetFile) != "" ? Path.GetFileName(cmd.TargetFile) : "index.html";
                    cmd.TargetFile = $"{_generatedProjectsPath}/{projectFolder}/{fileName}";
                    _queue.AddLog(cmd.Id, agentId, $"Yeni hedef: {cmd.TargetFile}", "info");
                }

                var fullPath = Path.Combine(_sourceRoot, cmd.TargetFile);
                _queue.AddLog(cmd.Id, agentId, $"Dosya yolu: {fullPath}", "info");

                var isNewFile = !File.Exists(fullPath);
                string oldContent;

                if (isNewFile)
                {
                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    _queue.AddLog(cmd.Id, agentId, $"Yeni dosya oluşturuluyor: {cmd.TargetFile}", "warn");
                    oldContent = "";
                }
                else
                {
                    oldContent = await File.ReadAllTextAsync(fullPath);
                    _queue.AddLog(cmd.Id, agentId, $"Dosya okundu ({oldContent.Length} karakter, {oldContent.Split('\n').Length} satır)", "success");
                }

                _queue.AddLog(cmd.Id, agentId, $"Değişiklik planlanıyor: {cmd.Text}", "info");

                var newContent = await GenerateNewContent(cmd.Id, cmd.Text, cmd.TargetFile!, oldContent);
                if (string.IsNullOrWhiteSpace(newContent))
                {
                    _queue.AddLog(cmd.Id, agentId, "AI yanıt vermedi, alternatif deneniyor...", "warn");
                    newContent = await GenerateNewContent(cmd.Id, cmd.Text, cmd.TargetFile!, oldContent);
                }

                if (string.IsNullOrWhiteSpace(newContent))
                {
                    _queue.AddLog(cmd.Id, agentId, "AI yanıt vermedi, değişiklik yapılamadı", "error");
                    _queue.MarkProcessed(cmd.Id, "AI returned empty content");
                    _performance.RecordFailure(agentId, "code_generation");
                    _memory.StoreEpisodic(agentId, cmd.Text, "code_fail", "AI yanit vermedi", "failed");
                    return new CodeExecutionResult { Success = false, Error = "AI yanıt vermedi" };
                }

                if (newContent == oldContent)
                {
                    _queue.AddLog(cmd.Id, agentId, "AI aynı içeriği döndü, farklı prompt ile tekrar deneniyor...", "warn");
                    var retryPrompt = $"Kullanici su degisikligi istedi: \"{cmd.Text}\". Dosyada MUTLAKA bir degisiklik yap. Sadece aciklama degil, gercek kod degisikligi yap.";
                    newContent = await GenerateNewContent(cmd.Id, retryPrompt, cmd.TargetFile!, oldContent) ?? oldContent;
                }

                var diff = ComputeDiff(cmd.TargetFile!, oldContent, newContent);
                _queue.AddLog(cmd.Id, agentId, $"Değişiklik: +{diff.LinesAdded} / -{diff.LinesRemoved} satır", "info");

                _queue.AddLog(cmd.Id, agentId, "Kod review basliyor...", "info");
                var reviewResult = await _review.ReviewAsync(cmd.Text, cmd.TargetFile!, oldContent, newContent);
                if (!reviewResult.Approved)
                {
                    _queue.AddLog(cmd.Id, agentId, $"Review reddedildi: {reviewResult.Feedback}", "warn");
                    File.WriteAllText(fullPath, oldContent);
                    _queue.AddLog(cmd.Id, agentId, "Degisiklik geri alindi, yeni deneme yapiliyor...", "info");
                    var fixPrompt = $"Kullanici: \"{cmd.Text}\". Onceki AI su hatayi yapti: {reviewResult.Feedback}. Bunu duzelt.";
                    var fixedContent = await GenerateNewContent(cmd.Id, fixPrompt, cmd.TargetFile!, oldContent);
                    if (!string.IsNullOrWhiteSpace(fixedContent) && fixedContent != oldContent)
                    {
                        newContent = fixedContent;
                        diff = ComputeDiff(cmd.TargetFile!, oldContent, newContent);
                        _queue.AddLog(cmd.Id, agentId, $"Duzeltilmis degisiklik: +{diff.LinesAdded} / -{diff.LinesRemoved} satir", "info");
                        await File.WriteAllTextAsync(fullPath, newContent);
                        _ = RegisterProjectAsync(cmd.TargetFile!);
                        _queue.AddLog(cmd.Id, agentId, "Duzeltilmis dosya yazildi", "success");
                    }
                    else
                    {
                        _queue.AddLog(cmd.Id, agentId, "Duzeltme de basarisiz, eski hal birakildi", "error");
                        _performance.RecordFailure(agentId, "code_review_fix");
                        _memory.StoreEpisodic(agentId, cmd.Text, "code_review_fail", reviewResult.Feedback, "failed");
                        return new CodeExecutionResult { Success = false, Error = $"Review reddedildi: {reviewResult.Feedback}" };
                    }
                }
                else
                {
                    _queue.AddLog(cmd.Id, agentId, $"Review onaylandi: {reviewResult.Feedback}", "success");
                    await File.WriteAllTextAsync(fullPath, newContent);
                    _ = RegisterProjectAsync(cmd.TargetFile!);
                    _queue.AddLog(cmd.Id, agentId, "Dosya basariyla guncellendi", "success");

                var diffLines = diff.DiffText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var shown = 0;
                foreach (var dl in diffLines)
                {
                    if (shown >= _maxDiffLines) { _queue.AddLog(cmd.Id, agentId, $"... ve {diffLines.Length - _maxDiffLines} satır daha", "info"); break; }
                    var dlLevel = dl.StartsWith('+') ? "success" : dl.StartsWith('-') ? "error" : "info";
                    _queue.AddLog(cmd.Id, agentId, dl, dlLevel);
                    shown++;
                }
                }

                var summary = $"{Path.GetFileName(cmd.TargetFile)} güncellendi (+{diff.LinesAdded}/-{diff.LinesRemoved} satır)";
                _queue.MarkProcessed(cmd.Id);
                _queue.AddLog(cmd.Id, agentId, summary, "success");

                _performance.RecordSuccess(agentId, cmd.ChangeType ?? "edit");
                _memory.StoreEpisodic(agentId, cmd.Text, "code_change", summary, "completed", diff.DiffText);

                return new CodeExecutionResult
                {
                    Success = true,
                    Summary = summary,
                    Diff = diff
                };
            }
            catch (Exception ex)
            {
                _queue.AddLog(cmd.Id, agentId, $"Hata: {ex.Message}", "error");
                _queue.MarkProcessed(cmd.Id, ex.Message);
                return new CodeExecutionResult { Success = false, Error = ex.Message };
            }
        }

        private async Task<string> GuessTargetFile(string text)
        {
            var lower = text.ToLowerInvariant();

            var explicitPath = TryExtractExplicitPath(text);
            if (explicitPath != null)
                return explicitPath;

            if (IsNewProjectRequest(lower))
            {
                var projectName = ExtractProjectName(text);
                return $"{_generatedProjectsPath}/{projectName}/index.html";
            }

            var prompt = $"Kullanıcı şöyle dedi: \"{text}\"\n\nBu komut hangi dosyayı değiştirmek istiyor? Sadece dosya yolunu yaz, başka bir şey yazma.\n\nNOT: Ajanlar sadece generated_projects/ klasörü altında çalışabilir. Mevcut proje dosyalarına (Backend/, frontend/) dokunulamaz.\n\nDosya yolu her zaman generated_projects/ ile başlamalıdır. Örn: generated_projects/proje-adi/index.html";

            if (!string.IsNullOrWhiteSpace(_openRouterKey))
            {
                var path = await TryOpenRouterGuess(prompt);
                if (path != null) return path;
            }

            if (!string.IsNullOrWhiteSpace(_geminiKey))
            {
                var path = await TryGeminiGuess(prompt);
                if (path != null) return path;
            }

            if (!string.IsNullOrWhiteSpace(_groqKey))
            {
                var path = await TryGroqGuess(prompt);
                if (path != null) return path;
            }

            return InferTargetFile(text);
        }

        private async Task<string?> TryGeminiGuess(string prompt)
        {
            try
            {
                var url = $"{_geminiBaseUrl}/v1beta/models/{_geminiModel}:generateContent?key={_geminiKey}";
                var payload = new { contents = new[] { new { parts = new[] { new { text = prompt } } } }, generationConfig = new { maxOutputTokens = 30, temperature = 0.1 } };
                var json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, httpContent);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var aiText = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
                    var path = aiText.Trim().Trim('`').Trim('"').Trim();
                    if (path.Length > 0) return path;
                }
            }
            catch { }
            return null;
        }

        private async Task<string?> TryGroqGuess(string prompt)
        {
            try
            {
                var payload = new
                {
                    model = _groqModel,
                    messages = new[] { new { role = "user", content = prompt } },
                    max_tokens = 30,
                    temperature = 0.1
                };
                var json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_groqBaseUrl}/openai/v1/chat/completions")
                {
                    Content = httpContent
                };
                request.Headers.Add("Authorization", $"Bearer {_groqKey}");
                var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    var path = text.Trim().Trim('`').Trim('"').Trim();
                    if (path.Length > 0) return path;
                }
            }
            catch { }
            return null;
        }

        private async Task<string?> TryOpenRouterGuess(string prompt)
        {
            try
            {
                var payload = new
                {
                    model = _openRouterModel,
                    messages = new[] { new { role = "user", content = prompt } },
                    max_tokens = 30,
                    temperature = 0.1
                };
                var json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_openRouterBaseUrl}/api/v1/chat/completions")
                {
                    Content = httpContent
                };
                request.Headers.Add("Authorization", $"Bearer {_openRouterKey}");
                request.Headers.Add("HTTP-Referer", _httpReferer);
                var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    var path = text.Trim().Trim('`').Trim('"').Trim();
                    if (path.Length > 0) return path;
                }
            }
            catch { }
            return null;
        }

        private string InferTargetFile(string text)
        {
            var lower = text.ToLowerInvariant();

            var extractedPath = TryExtractExplicitPath(text);
            if (extractedPath != null)
                return extractedPath;

            var activeProj = _memory.GetActiveProject();
            if (IsNewProjectRequest(lower))
            {
                if (!string.IsNullOrWhiteSpace(activeProj))
                    return $"{_generatedProjectsPath}/{activeProj}/index.html";
                return $"{_generatedProjectsPath}/yeni-proje/index.html";
            }
            if (!string.IsNullOrWhiteSpace(activeProj))
                return $"{_generatedProjectsPath}/{activeProj}/index.html";
            return $"{_generatedProjectsPath}/yeni-proje/index.html";
        }

        private string? TryExtractExplicitPath(string text)
        {
            var normalized = text.Replace("\\", "/");
            var lower = normalized.ToLowerInvariant();
            var activeProj = _memory.GetActiveProject();

            var genProjectsMatch = Regex.Match(lower, @"generated_projects/([\w-]+(?:/[\w-]+)*)");
            var mdMatch = Regex.Match(lower, @"\b([\w-]+\.md)\b");

            if (genProjectsMatch.Success)
            {
                var path = genProjectsMatch.Groups[1].Value;
                if (path.Contains('/') && mdMatch.Success && !mdMatch.Value.StartsWith("index"))
                {
                    var folder = path;
                    var file = mdMatch.Groups[1].Value;
                    return $"{_generatedProjectsPath}/{folder}/{file}";
                }
                if (path.Contains('/'))
                    return $"{_generatedProjectsPath}/{path}";
                var name = path;
                if (name.Length > 0 && name != "yeni-proje")
                    return $"{_generatedProjectsPath}/{name}/index.html";
            }

            if (mdMatch.Success && !mdMatch.Value.StartsWith("index"))
            {
                var fileName = mdMatch.Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(activeProj))
                    return $"{_generatedProjectsPath}/{activeProj}/{fileName}";
                return $"{_generatedProjectsPath}/{fileName}";
            }

            var rootMatch = Regex.Match(lower, @"([\w-]+)\s*(?:rootu|klasörü|dizini|klasoru|dizini)\s*(?:bu|)");
            if (rootMatch.Success)
            {
                var name = rootMatch.Groups[1].Value;
                if (name.Length > 0 && !name.Contains("proje"))
                {
                    if (!string.IsNullOrWhiteSpace(activeProj))
                        return $"{_generatedProjectsPath}/{activeProj}/index.html";
                    return $"{_generatedProjectsPath}/{name}/index.html";
                }
            }

            return null;
        }

        private static bool IsNewProjectRequest(string lower)
        {
            if (lower.Contains("site") || lower.Contains("website") ||
                lower.Contains("sayfa") || lower.Contains("page") ||
                lower.Contains("web") || lower.Contains("uygulama") ||
                lower.Contains("app") || lower.Contains("uygulaması") ||
                lower.Contains("uygulamasini"))
                return true;

            var projectRefs = new[] { "proje", "project" };
            foreach (var pr in projectRefs)
            {
                var idx = lower.IndexOf(pr, StringComparison.Ordinal);
                while (idx >= 0)
                {
                    var end = idx + pr.Length;
                    if (end >= lower.Length || !char.IsLetter(lower[end]))
                        return true;
                    idx = lower.IndexOf(pr, end, StringComparison.Ordinal);
                }
            }
            return false;
        }

        private static string ExtractProjectName(string text)
        {
            var normalized = text.Replace("\\", "/");
            var lower = normalized.ToLowerInvariant();

            var genProjectsMatch = Regex.Match(lower, @"generated_projects/([\w-]+)");
            if (genProjectsMatch.Success)
                return genProjectsMatch.Groups[1].Value;

            var rootMatch = Regex.Match(lower, @"([\w-]+)\s*(?:rootu|klasörü|dizini|klasoru|dizini)");
            if (rootMatch.Success)
                return Slugify(rootMatch.Groups[1].Value);

            var patterns = new[] { " isimli", " adli", " adinda", " adında", " isminde", " adında bir", " isminde bir" };
            foreach (var pat in patterns)
            {
                var idx = lower.IndexOf(pat, StringComparison.Ordinal);
                if (idx > 5)
                {
                    var start = Math.Max(0, idx - 50);
                    var before = text[start..idx].Trim();
                    var words = before.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (var i = words.Length - 1; i >= 0; i--)
                    {
                        var w = words[i].ToLowerInvariant().Trim('.', ',', '!', '?', '"', '\'');
                        if (w.Length > 2 && w != "bir" && w != "ile" && w != "ve" && w != "veya" && w != "icin" && w != "için")
                            return Slugify(w);
                    }
                }
            }
            return "yeni-proje";
        }

        private static string Slugify(string text)
        {
            var trMap = new Dictionary<char, char> {
                { 'ç', 'c' }, { 'ğ', 'g' }, { 'ı', 'i' }, { 'ö', 'o' }, { 'ş', 's' }, { 'ü', 'u' },
                { 'Ç', 'c' }, { 'Ğ', 'g' }, { 'İ', 'i' }, { 'Ö', 'o' }, { 'Ş', 's' }, { 'Ü', 'u' }
            };
            var sb = new StringBuilder();
            foreach (var c in text.ToLowerInvariant().Trim())
            {
                if (trMap.ContainsKey(c)) sb.Append(trMap[c]);
                else if (char.IsLetterOrDigit(c) || c == '-') sb.Append(c);
                else if (c == ' ') sb.Append('-');
            }
            return sb.ToString();
        }

        private async Task<string> GenerateNewContent(string cmdId, string command, string filePath, string oldContent)
        {
            _queue.AddLog(cmdId, "agent", "Opencode ile kod değişikliği çağrılıyor...", "info");

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var isNewProject = filePath.StartsWith(_generatedProjectsPath) && string.IsNullOrEmpty(oldContent) && ext != ".md";

            if (isNewProject)
            {
                _queue.AddLog(cmdId, "agent", "Yeni proje oluşturuluyor...", "info");
                var fullProjectPath = Path.Combine(_sourceRoot, Path.GetDirectoryName(filePath)!);
                var projectPrompt = $@"Yeni bir web sitesi/proje oluşturmam gerekiyor.

Kullanıcının isteği: {command}

Proje klasörü: {fullProjectPath}

Bu klasörün altinda HTML, CSS, JS dosyalarini olustur. index.html dosyasina eksiksiz bir web sayfasi yap.
Tum dosyalari calisir ve eksiksiz hazirla.";
                var newProjectResult = await _opencode.AskRawAsync(projectPrompt);
                var mainFile = Path.Combine(_sourceRoot, filePath);
                if (File.Exists(mainFile))
                    return await File.ReadAllTextAsync(mainFile);
                if (!string.IsNullOrWhiteSpace(newProjectResult))
                {
                    var cleaned = ExtractCode(newProjectResult, ".html");
                    if (cleaned.Length > 0)
                    {
                        _queue.AddLog(cmdId, "agent", $"Yanıttan {cleaned.Length} karakter kod alındı", "success");
                        return cleaned;
                    }
                }
                _queue.AddLog(cmdId, "agent", "Opencode yanıt vermedi, OpenRouter deneniyor...", "warn");
                var fallbackResult = await TryOpenRouterChange(cmdId, projectPrompt, ".html");
                if (fallbackResult != null) return fallbackResult;
                _queue.AddLog(cmdId, "agent", "OpenRouter yanıt vermedi, Gemini deneniyor...", "warn");
                fallbackResult = await TryGeminiChange(cmdId, projectPrompt, ".html");
                if (fallbackResult != null) return fallbackResult;
                _queue.AddLog(cmdId, "agent", "Gemini yanıt vermedi, Groq deneniyor...", "warn");
                fallbackResult = await TryGroqChange(cmdId, projectPrompt, ".html");
                if (fallbackResult != null) return fallbackResult;
                return "";
            }

            var fullPath = Path.Combine(_sourceRoot, filePath);
            var langHint = ext switch
            {
                ".js" or ".jsx" => "JavaScript/React",
                ".cs" => "C#",
                ".json" => "JSON",
                ".css" => "CSS",
                _ => "code"
            };

            var prompt = $@"Aşağıdaki {langHint} dosyasını değiştirmem gerekiyor.

Kullanıcının isteği: {command}

Dosya yolu: {filePath}

Mevcut dosya içeriği:
{oldContent}

Sadece değiştirilmiş dosyanın TAMAMINI yaz. SADECE KOD yaz, aciklama EKLEME. Markdown kullanma (``` EKLEME). Eksiksiz ve calisir olmali.";

            _queue.AddLog(cmdId, "agent", "Opencode API çağrılıyor...", "info");
            var opencodeResult = await _opencode.AskRawAsync(prompt);

            if (File.Exists(fullPath))
            {
                var newContent = await File.ReadAllTextAsync(fullPath);
                if (newContent != oldContent)
                {
                    _queue.AddLog(cmdId, "agent", $"Opencode dosyayı düzenledi ({newContent.Length} karakter)", "success");
                    return newContent;
                }
            }

            _queue.AddLog(cmdId, "agent", "Opencode dosyayı değiştirmedi, yanıt metninden çıkartılıyor...", "warn");
            if (!string.IsNullOrWhiteSpace(opencodeResult))
            {
                var cleaned = ExtractCode(opencodeResult, ext);
                if (cleaned.Length > 0 && cleaned != oldContent)
                {
                    _queue.AddLog(cmdId, "agent", $"Yanıttan {cleaned.Length} karakter kod alındı", "success");
                    return cleaned;
                }
            }

            _queue.AddLog(cmdId, "agent", "Opencode yanıt vermedi, OpenRouter deneniyor...", "warn");
            var result = await TryOpenRouterChange(cmdId, prompt, Path.GetExtension(filePath).ToLowerInvariant());
            if (result != null) return result;

            _queue.AddLog(cmdId, "agent", "OpenRouter yanıt vermedi, Gemini deneniyor...", "warn");
            result = await TryGeminiChange(cmdId, prompt, Path.GetExtension(filePath).ToLowerInvariant());
            if (result != null) return result;

            _queue.AddLog(cmdId, "agent", "Gemini yanıt vermedi, Groq deneniyor...", "warn");
            result = await TryGroqChange(cmdId, prompt, Path.GetExtension(filePath).ToLowerInvariant());
            if (result != null) return result;

            _queue.AddLog(cmdId, "agent", "OpenRouter yanıt vermedi", "error");
            return "";
        }

        private async Task<string?> TryGeminiChange(string cmdId, string prompt, string ext)
        {
            if (string.IsNullOrWhiteSpace(_geminiKey)) return null;
            try
            {
                var url = $"{_geminiBaseUrl}/v1beta/models/{_geminiModel}:generateContent?key={_geminiKey}";
                var payload = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } },
                    generationConfig = new { maxOutputTokens = 4096, temperature = 0.3 }
                };
                var json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(url, httpContent);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var rawText = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";
                    var cleaned = ExtractCode(rawText, ext);
                    if (cleaned.Length > 0) return cleaned;
                }
                else
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    _queue.AddLog(cmdId, "agent", $"Gemini hatası ({(int)response.StatusCode}): {errBody[..Math.Min(200, errBody.Length)]}", "error");
                }
            }
            catch (Exception ex)
            {
                _queue.AddLog(cmdId, "agent", $"Gemini hatası: {ex.Message}", "error");
            }
            return null;
        }

        private async Task<string?> TryGroqChange(string cmdId, string prompt, string ext)
        {
            if (string.IsNullOrWhiteSpace(_groqKey)) return null;
            try
            {
                var payload = new
                {
                    model = _groqModel,
                    messages = new[] { new { role = "user", content = prompt } },
                    max_tokens = 4096,
                    temperature = 0.3
                };
                var json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_groqBaseUrl}/openai/v1/chat/completions")
                {
                    Content = httpContent
                };
                request.Headers.Add("Authorization", $"Bearer {_groqKey}");
                var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var rawText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    var cleaned = ExtractCode(rawText, ext);
                    if (cleaned.Length > 0) return cleaned;
                }
                else
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    _queue.AddLog(cmdId, "agent", $"Groq hatası ({(int)response.StatusCode}): {errBody[..Math.Min(200, errBody.Length)]}", "error");
                }
            }
            catch (Exception ex)
            {
                _queue.AddLog(cmdId, "agent", $"Groq hatası: {ex.Message}", "error");
            }
            return null;
        }

        private async Task<string?> TryOpenRouterChange(string cmdId, string prompt, string ext)
        {
            if (string.IsNullOrWhiteSpace(_openRouterKey)) return null;
            try
            {
                var payload = new
                {
                    model = _openRouterModel,
                    messages = new[] { new { role = "user", content = prompt } },
                    max_tokens = 4096,
                    temperature = 0.3
                };
                var json = JsonSerializer.Serialize(payload);
                var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_openRouterBaseUrl}/api/v1/chat/completions")
                {
                    Content = httpContent
                };
                request.Headers.Add("Authorization", $"Bearer {_openRouterKey}");
                request.Headers.Add("HTTP-Referer", _httpReferer);
                var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(body);
                    var rawText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                    var cleaned = ExtractCode(rawText, ext);
                    if (cleaned.Length > 0) return cleaned;
                }
                else
                {
                    var errBody = await response.Content.ReadAsStringAsync();
                    _queue.AddLog(cmdId, "agent", $"OpenRouter hatası ({(int)response.StatusCode}): {errBody[..Math.Min(200, errBody.Length)]}", "error");
                }
            }
            catch (Exception ex)
            {
                _queue.AddLog(cmdId, "agent", $"OpenRouter hatası: {ex.Message}", "error");
            }
            return null;
        }

        private static string ExtractCode(string raw, string ext)
        {
            var codeBlockStart = "```" + ext.TrimStart('.');
            var start = raw.IndexOf(codeBlockStart);
            if (start >= 0)
            {
                start += codeBlockStart.Length;
                var end = raw.IndexOf("```", start);
                if (end > start) return raw[start..end].Trim();
            }

            start = raw.IndexOf("```");
            if (start >= 0)
            {
                start = raw.IndexOf('\n', start) + 1;
                var end = raw.IndexOf("```", start);
                if (end > start) return raw[start..end].Trim();
            }

            return raw.Trim();
        }

        private static FileDiff ComputeDiff(string filePath, string oldContent, string newContent)
        {
            var oldLines = oldContent.Split('\n');
            var newLines = newContent.Split('\n');

            var added = 0;
            var removed = 0;

            var oldSet = new HashSet<string>(oldLines);
            var newSet = new HashSet<string>(newLines);

            foreach (var l in newLines)
                if (!oldSet.Contains(l)) added++;

            foreach (var l in oldLines)
                if (!newSet.Contains(l)) removed++;

            var sb = new StringBuilder();
            var oldIdx = 0;
            var newIdx = 0;
            while (oldIdx < oldLines.Length || newIdx < newLines.Length)
            {
                if (oldIdx < oldLines.Length && newIdx < newLines.Length && oldLines[oldIdx] == newLines[newIdx])
                {
                    sb.AppendLine($" {oldLines[oldIdx]}");
                    oldIdx++;
                    newIdx++;
                }
                else if (oldIdx < oldLines.Length && (!newSet.Contains(oldLines[oldIdx]) || newIdx >= newLines.Length))
                {
                    sb.AppendLine($"-{oldLines[oldIdx]}");
                    oldIdx++;
                }
                else if (newIdx < newLines.Length)
                {
                    sb.AppendLine($"+{newLines[newIdx]}");
                    newIdx++;
                }
                else break;
            }

            return new FileDiff
            {
                FilePath = filePath,
                LinesAdded = added,
                LinesRemoved = removed,
                DiffText = sb.ToString()
            };
        }

        private async Task RegisterProjectAsync(string filePath)
        {
            try
            {
                if (!filePath.StartsWith(_generatedProjectsPath + "/", StringComparison.OrdinalIgnoreCase))
                    return;

                var relative = filePath[_generatedProjectsPath.Length..].TrimStart('/');
                var slashIdx = relative.IndexOf('/');
                if (slashIdx <= 0) return;

                var projectSlug = relative[..slashIdx];
                if (string.IsNullOrWhiteSpace(projectSlug) || projectSlug == "yeni-proje")
                    return;

                var existing = await _db.Projects.FirstOrDefaultAsync(p => p.Slug == projectSlug);
                if (existing != null)
                {
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var displayName = string.Join(' ', projectSlug.Split('-', '_')
                        .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
                    _db.Projects.Add(new Project
                    {
                        Name = displayName,
                        Slug = projectSlug,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Status = "active"
                    });
                }
                await _db.SaveChangesAsync();
            }
            catch { }
        }
    }
}
