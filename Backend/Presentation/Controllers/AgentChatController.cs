using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSFast.Application.Services;
using SaaSFast.Infrastructure.Data;

namespace SaaSFast.Presentation.Controllers
{
    [ApiController]
    [Route("api/agent")]
    public class AgentChatController : ControllerBase
    {
        private readonly AiService _ai;
        private readonly VoiceService _voice;
        private readonly CommandQueueService _queue;
        private readonly CodeExecutorService _executor;
        private readonly AgentMemoryService _memory;
        private readonly AppDbContext _db;

        private static readonly string[] CodeVerbs = {
            "değiştir", "güncelle", "yenile", "ayarla",
            "yap", "ekle", "sil", "kaldır", "çıkar",
            "büyüt", "küçült", "taşı",
            "yaz", "kaydet", "kaydedin",
            "oluştur", "oluşturun", "hazırla", "hazırlayın",
            "topla", "toplayın",
            "indir", "yükle", "kur", "çalıştır",
            "dene", "test", "düzenle", "göster",
            "oku", "sorgula",
            "ekleme", "güncelleme", "silme",
            "doldur"
        };

        private static bool IsExplicitCodeCommand(string message)
        {
            var lower = message.ToLowerInvariant();
            foreach (var v in CodeVerbs)
            {
                if (lower.Contains(v))
                    return true;
            }
            return false;
        }

        public AgentChatController(AiService ai, VoiceService voice, CommandQueueService queue, CodeExecutorService executor, AgentMemoryService memory, AppDbContext db)
        {
            _ai = ai;
            _voice = voice;
            _queue = queue;
            _executor = executor;
            _memory = memory;
            _db = db;
        }

        private static readonly Dictionary<string, (string tr, string en)> DefaultResponses = new()
        {
            ["ceo"] = ("Emir bekliyorum. Ne yapalım?", "Awaiting orders. What's the task?"),
            ["product"] = ("Yeni bir fikir mi var?", "Got a new idea?"),
            ["research"] = ("Verileri inceliyorum. Ne araştırayım?", "Analyzing data. What should I research?"),
            ["architect"] = ("Sistem hazır. Ne tasarlayalım?", "System's ready. What are we designing?"),
            ["backend"] = ("Backend bekliyor. Ne kodlayalım?", "Backend ready. What needs coding?"),
            ["frontend"] = ("Frontend hazır. Ne yapalım?", "Frontend ready. What should I build?"),
            ["qa"] = ("Testlere hazırım. Neyi test edeyim?", "Ready to test. What should I verify?"),
            ["devops"] = ("Altyapı sağlam. Ne dağıtalım?", "Infrastructure solid. What's the deploy?"),
        };

        private static string GetDefaultResponse(string agentId, bool tr)
        {
            return DefaultResponses.TryGetValue(agentId, out var r)
                ? (tr ? r.tr : r.en)
                : (tr ? "Nasıl yardımcı olabilirim?" : "How can I help?");
        }

        private static bool IsProjectCreateConfirmation(string lowerMessage)
        {
            var confirmWords = new[] { "evet", "tamam", "olur", "aç", "create", "yes", "ok" };
            return confirmWords.Any(w => lowerMessage.Contains(w))
                && !lowerMessage.Contains("odaklan")
                && !lowerMessage.Contains("araştır")
                && !lowerMessage.Contains("sil");
        }

        private static string? TryExtractProjectCreationName(string msg)
        {
            // Pattern: "X projesi oluştur/yap/başlat/kur/hazırla" or "X projesini oluştur"
            var m1 = Regex.Match(msg, @"([\w-]+(?:_[\w-]+)*)\s*(?:projesi|projesine|proje|projeyi|projesini)\s+(?:adı\s*altında\s*bir\s*proje\s*)?(?:oluştur|yap|başlat|kur|hazırla|yapalım|oluşturalım|başlatalım|kuralım|hazırlayalım)", RegexOptions.IgnoreCase);
            if (m1.Success)
            {
                var raw = m1.Groups[1].Value;
                return raw.EndsWith("projesi", StringComparison.OrdinalIgnoreCase) ? raw : raw + "_projesi";
            }

            // Pattern: "X adı altında bir proje oluştur" (where X doesn't have "projesi")
            var m2 = Regex.Match(msg, @"([\w-]+(?:_[\w-]+)*)\s+(?:adı|adi)\s*altında\s*bir\s*proje\s*(?:oluştur|yap|başlat|kur|hazırla|yapalım|oluşturalım|başlatalım|kuralım|hazırlayalım)", RegexOptions.IgnoreCase);
            if (m2.Success)
            {
                var raw = m2.Groups[1].Value;
                return raw.EndsWith("projesi", StringComparison.OrdinalIgnoreCase) ? raw : raw + "_projesi";
            }

            // Pattern: "X projesi yapalım" (bare project name before creation verb)
            var m3 = Regex.Match(msg, @"([\w-]+(?:projesi|proje))\s+(?:yapalım|yapalim|oluşturalım|olusturalim|başlatalım|baslatalim|kuralım|kurallim|hazırlayalım|hazirlayalim)\b", RegexOptions.IgnoreCase);
            if (m3.Success)
            {
                var raw = m3.Groups[1].Value;
                return raw.EndsWith("projesi", StringComparison.OrdinalIgnoreCase) ? raw : raw + "_projesi";
            }

            return null;
        }

        private async Task<string> CreateProjectInDb(string slug, string userMessage)
        {
            var displayName = string.Join(' ', slug.Split('-', '_')
                .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));

            _db.Projects.Add(new SaaSFast.Domain.Entities.Project
            {
                Name = displayName,
                Slug = slug,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Status = "active"
            });
            await _db.SaveChangesAsync();
            _memory.SetActiveProject(slug);

            var fullDir = Path.Combine(Directory.GetCurrentDirectory(), "generated_projects", slug);
            var fullPath = Path.Combine(fullDir, $"{slug}.md");
            if (!Directory.Exists(fullDir)) Directory.CreateDirectory(fullDir);
            if (!System.IO.File.Exists(fullPath))
            {
                var mdContent = $"# {displayName}\n\n## Proje Amaci\n{userMessage}\n\n## Hedefler\n-\n\n## Notlar\n-";
                await System.IO.File.WriteAllTextAsync(fullPath, mdContent);
            }

            return slug;
        }

        private static bool MessageTargetsMdInGenerated(string message)
        {
            var lower = message.ToLowerInvariant();

            // Explicit .md filename reference
            if (Regex.IsMatch(lower, @"[\w-]+\.md\b"))
                return true;

            // "md dosya" / "md dosyası" pattern
            if (lower.Contains("md dosya"))
                return true;

            // "dosya" referenced with a write verb (doldur, yaz, etc.) — assume .md in generated_projects
            if (lower.Contains("dosya") || lower.Contains("dosyayı") || lower.Contains("dosyası"))
                return true;

            return false;
        }

        private async Task<string?> AutoCreateMdFromResponse(string content, string? activeProject)
        {
            var lower = content.ToLowerInvariant();
            var activeSlug = activeProject ?? _memory.GetActiveProject();
            if (string.IsNullOrWhiteSpace(activeSlug) && !lower.Contains("generated_projects"))
                return null;

            // Pattern: generated_projects/{project}/{file}.md
            var explicitMatch = Regex.Match(lower, @"generated_projects/([\w-]+)/([\w-]+\.md)\b");
            if (explicitMatch.Success)
            {
                var project = explicitMatch.Groups[1].Value;
                var fileName = explicitMatch.Groups[2].Value;
                var fullDir = Path.Combine(Directory.GetCurrentDirectory(), "generated_projects", project);
                var fullPath = Path.Combine(fullDir, fileName);
                if (!Directory.Exists(fullDir)) Directory.CreateDirectory(fullDir);
                if (!System.IO.File.Exists(fullPath))
                {
                    var displayName = string.Join(' ', Path.GetFileNameWithoutExtension(fileName).Split('-', '_')
                        .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
                    await System.IO.File.WriteAllTextAsync(fullPath, $"# {displayName}\n\n## Icerik\n{content}\n\n## Notlar\n-");
                    return $"generated_projects/{project}/{fileName}";
                }
            }

            // Pattern: mentions .md filename and active project exists
            if (!string.IsNullOrWhiteSpace(activeSlug))
            {
                var mdFileMatch = Regex.Match(lower, @"([\w-]+\.md)\b");
                if (mdFileMatch.Success)
                {
                    var fileName = mdFileMatch.Groups[1].Value;
                    var fullDir = Path.Combine(Directory.GetCurrentDirectory(), "generated_projects", activeSlug);
                    var fullPath = Path.Combine(fullDir, fileName);
                    if (!Directory.Exists(fullDir)) Directory.CreateDirectory(fullDir);
                    if (!System.IO.File.Exists(fullPath))
                    {
                        var displayName = string.Join(' ', Path.GetFileNameWithoutExtension(fileName).Split('-', '_')
                            .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
                        await System.IO.File.WriteAllTextAsync(fullPath, $"# {displayName}\n\n## Icerik\n{content}\n\n## Notlar\n-");
                        return $"generated_projects/{activeSlug}/{fileName}";
                    }
                }
            }

            return null;
        }

        private static bool IsCorrection(string lowerMessage)
        {
            if (lowerMessage.Contains("değil") || lowerMessage.Contains("degil"))
                return true;
            if (lowerMessage.Contains("yanlış") || lowerMessage.Contains("yanlis"))
                return true;
            if (lowerMessage.Contains("düzelt") || lowerMessage.Contains("duzelt"))
                return true;
            if (lowerMessage.Contains("doğrusu") || lowerMessage.Contains("dogrusu"))
                return true;
            return false;
        }

        private static string? ExtractCorrectionProject(string message)
        {
            // Match quoted text: 'orumcek_projesi' or "orumcek_projesi"
            var quoteMatch = Regex.Match(message, @"['""]([\w-]+)['""]");
            if (quoteMatch.Success)
            {
                var name = quoteMatch.Groups[1].Value;
                if (name.Length > 1 && !name.Contains("proje"))
                    return name;
            }

            // Match "X değil Y" → extract Y
            var degilMatch = Regex.Match(message, @"değil\s+(?:dosya\s+)?(?:adı\s+)?(?:tam\s+)?(?:olarak\s+)?(?:bu\s+)?['""]?([\w-]+)['""]?", RegexOptions.IgnoreCase);
            if (degilMatch.Success)
            {
                var name = degilMatch.Groups[1].Value;
                if (name.Length > 1 && !name.Contains("proje"))
                    return name;
            }

            return null;
        }

        private static string Slugify(string text)
        {
            var trMap = new Dictionary<char, char> {
                { 'ç', 'c' }, { 'ğ', 'g' }, { 'ı', 'i' }, { 'ö', 'o' }, { 'ş', 's' }, { 'ü', 'u' },
                { 'Ç', 'c' }, { 'Ğ', 'g' }, { 'İ', 'i' }, { 'Ö', 'o' }, { 'Ş', 's' }, { 'Ü', 'u' }
            };
            var sb = new System.Text.StringBuilder();
            foreach (var c in text.ToLowerInvariant().Trim())
            {
                if (trMap.ContainsKey(c)) sb.Append(trMap[c]);
                else if (char.IsLetterOrDigit(c) || c == '-') sb.Append(c);
                else if (c == ' ') sb.Append('-');
            }
            return sb.ToString();
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AiRequest request)
        {
            var lang = request.Language == "tr";
            string? focusProject = null;

            var cleanedMessage = request.Message.Trim().TrimEnd('.', ',', '!', '?', ':', ';');
            var lowerMessage = cleanedMessage.ToLowerInvariant();

            string? pendingProject = _memory.GetPendingProject();
            string? projectCreated = null;

            // 1. Pending project confirmation
            if (pendingProject != null && IsProjectCreateConfirmation(lowerMessage))
            {
                _memory.ClearPendingProject();
                projectCreated = await CreateProjectInDb(pendingProject, request.Message);
            }

            // 2. Direct project creation (user provides name + verb in same message)
            if (projectCreated == null && pendingProject == null)
            {
                var directName = TryExtractProjectCreationName(cleanedMessage);
                if (directName != null)
                {
                    var slug = Slugify(directName);
                    var dbProject = await _db.Projects.FirstOrDefaultAsync(p => p.Slug == slug);
                    if (dbProject == null)
                    {
                        projectCreated = await CreateProjectInDb(slug, request.Message);
                    }
                    else
                    {
                        _memory.SetActiveProject(slug);
                        focusProject = slug;
                    }
                }
            }

            // 2b. "aktif projemizin adı X" / "proje adı X" → set active project
            if (projectCreated == null && focusProject == null && pendingProject == null)
            {
                var nameMatch = Regex.Match(cleanedMessage, @"(?:aktif\s+proje\w*\s+|projemizin\s+|projenin\s+)?(?:adı|adi)\s+([\w-]+(?:\s+[\w-]+)*)", RegexOptions.IgnoreCase);
                if (nameMatch.Success)
                {
                    var rawName = nameMatch.Groups[1].Value.Trim();
                    if (rawName.Length > 2)
                    {
                        var slug = Slugify(rawName);
                        // Normalize: "orumcek-projesi" → "orumcekprojesi"
                        slug = slug.Replace("-projesi", "projesi").Replace("-proje", "proje");
                        var dbProject = await _db.Projects.FirstOrDefaultAsync(p => p.Slug == slug);
                        if (dbProject == null)
                        {
                            projectCreated = await CreateProjectInDb(slug, request.Message);
                        }
                        else
                        {
                            _memory.SetActiveProject(slug);
                            focusProject = slug;
                        }
                    }
                }
            }

            // 3. Focus match (odaklan, bağlan, geç) — only if no project was created above
            string? focusError = null;
            if (projectCreated == null)
            {
                var focusMatch = Regex.Match(cleanedMessage, @"([\w-]+(?:_[\w-]+)*)\s*(?:projesi|projesine|proje|projeye|focus)?\s*(?:ne|a|e|ye|ya|)?\s*(?:odaklan|bağlan|baglan|geç|gec)", RegexOptions.IgnoreCase);
                if (focusMatch.Success)
                {
                    var rawName = focusMatch.Groups[1].Value;
                    rawName = Regex.Replace(rawName, @"_(projesi|projesine|proje|projeye)$", "", RegexOptions.IgnoreCase);
                    if (Regex.IsMatch(cleanedMessage, @"\s+projesi|\s+projesine|\s+proje\b|\s+projeye", RegexOptions.IgnoreCase)
                        && !rawName.Contains("projesi", StringComparison.OrdinalIgnoreCase))
                        rawName += "_projesi";
                    focusProject = Slugify(rawName);

                    var dbProject = await _db.Projects.FirstOrDefaultAsync(p => p.Slug == focusProject);
                    if (dbProject == null)
                    {
                        _memory.SetPendingProject(focusProject);
                        focusError = lang
                            ? $"Proje \"{focusProject}\" bulunamadı. generated_projects/{focusProject}/ yolunda oluşturayım mı? (Evet derseniz oluşturup odaklanırım)"
                            : $"Project \"{focusProject}\" not found. Create at generated_projects/{focusProject}/? (Say yes to create and focus)";
                        focusProject = null;
                    }
                    else
                    {
                        _memory.ClearPendingProject();
                        _memory.SetActiveProject(focusProject);
                    }
                }
            }
            else
            {
                focusProject = projectCreated;
                _memory.ClearPendingProject();
            }

            var isCorrection = IsCorrection(lowerMessage) && focusProject == null && pendingProject == null;
            if (isCorrection)
            {
                var correctionProject = ExtractCorrectionProject(cleanedMessage);
                if (correctionProject != null)
                {
                    var correctedSlug = Slugify(correctionProject);
                    var dbProject = await _db.Projects.FirstOrDefaultAsync(p => p.Slug == correctedSlug);
                    if (dbProject != null)
                    {
                        _memory.ClearPendingProject();
                        _memory.SetActiveProject(correctedSlug);
                        focusProject = correctedSlug;
                    }
                    else
                    {
                        _memory.SetPendingProject(correctedSlug);
                        focusError = lang
                            ? $"Proje \"{correctedSlug}\" bulunamadı. generated_projects/{correctedSlug}/ yolunda oluşturayım mı?"
                            : $"Project \"{correctedSlug}\" not found. Create at generated_projects/{correctedSlug}/?";
                    }
                }
            }

            var result = await _ai.AskAsync(request);
            var content = result?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                if (projectCreated != null)
                    content = lang ? $"Proje \"{projectCreated}\" oluşturuldu ve aktif edildi." : $"Project \"{projectCreated}\" created and focused.";
                else if (focusError != null)
                    content = focusError;
                else if (focusProject != null)
                    content = lang ? $"Aktif proje \"{focusProject}\" olarak ayarlandı." : $"Active project set to \"{focusProject}\".";
                else
                    content = GetDefaultResponse(request.AgentId, lang);
            }
            var response = new Dictionary<string, object>
            {
                ["content"] = content ?? "",
                ["agentId"] = result?.AgentId ?? "",
                ["agentName"] = result?.AgentName ?? "",
                ["voiceId"] = result?.VoiceId ?? "",
                ["room"] = result?.Room ?? ""
            };

            if (focusProject != null)
                response["activeProject"] = focusProject;

            // Strateji Odasi ajanlari (ceo, product, research, architect) varsayilan olarak kod yazamaz
            var strategyRoomAgents = new[] { "ceo", "product", "research", "architect" };
            var isStrategyAgent = strategyRoomAgents.Contains(request.AgentId);

            // Proje adi soruluyorsa veya onay bekleniyorsa kod calistirma
            var awaitingProjectName = focusError != null || (pendingProject != null && projectCreated == null);

            var isCodeChange = IsExplicitCodeCommand(request.Message) && !isCorrection && !awaitingProjectName;

            // Strateji ajanlari sadece generated_projects/ altinda .md dosyasi olusturabilir
            if (isStrategyAgent && isCodeChange && !MessageTargetsMdInGenerated(request.Message))
                isCodeChange = false;

            if (isCodeChange)
            {
                var cmd = _queue.Enqueue(request.Message);
                cmd.ActiveAgentId = request.AgentId;

                var execResult = await _executor.ExecuteAsync(cmd);
                response["command"] = new
                {
                    cmd.Id,
                    cmd.Text,
                    cmd.TargetFile,
                    cmd.ChangeType,
                    cmd.Status,
                    Result = new
                    {
                        execResult.Success,
                        execResult.Summary,
                        Diff = execResult.Diff?.DiffText,
                        execResult.Error
                    }
                };

                var agentDisplayName = result?.AgentName ?? request.AgentId;
                response["completionMessage"] = execResult.Success
                    ? $"İşimi bitirdim. {execResult.Summary}"
                    : $"İşlem başarısız oldu. {execResult.Error}";
                response["completionAgentId"] = request.AgentId;
            }

            // AI yanitinda generated_projects/ altinda .md yolu gecerse otomatik olustur
            if (!isCodeChange && !string.IsNullOrWhiteSpace(content))
            {
                var autoMd = await AutoCreateMdFromResponse(content, focusProject ?? projectCreated);
                if (autoMd != null)
                {
                    response["autoFileCreated"] = autoMd;
                    content += $"\n\n✅ `{autoMd}` dosyasi otomatik olusturuldu.";
                    response["content"] = content;
                }
            }

            if (result != null)
            {
                var hadChange = isCodeChange || response.ContainsKey("autoFileCreated");
                _memory.StoreConversation(request.Message, request.AgentId, result.Content, request.Room, hadChange);
            }

            return Ok(response);
        }

        [HttpPost("chime")]
        public async Task<IActionResult> Chime([FromBody] ChimeRequest request)
        {
            var tr = request.Language == "tr";
            var result = await _ai.ChimeAsync(request.Room, request.History ?? new(), tr);
            if (result == null) return Ok(new { agentId = (string?)null });
            return Ok(result);
        }

        [HttpGet("voices")]
        public IActionResult Voices()
        {
            return Ok(_voice.GetAllVoices());
        }

        [HttpGet("project/active")]
        public IActionResult GetActiveProject()
        {
            var project = _memory.GetActiveProject();
            return Ok(new { activeProject = project });
        }
    }
}
