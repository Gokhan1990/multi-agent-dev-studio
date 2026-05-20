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
            "ekleme", "güncelleme", "silme"
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
            var confirmWords = new[] { "evet", "oluştur", "tamam", "olur", "yap", "aç", "create", "yes", "ok" };
            return confirmWords.Any(w => lowerMessage.Contains(w))
                && !lowerMessage.Contains("odaklan")
                && !lowerMessage.Contains("araştır")
                && !lowerMessage.Contains("sil");
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

            if (pendingProject != null && IsProjectCreateConfirmation(lowerMessage))
            {
                var displayName = string.Join(' ', pendingProject.Split('-', '_')
                    .Select(w => w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
                _db.Projects.Add(new SaaSFast.Domain.Entities.Project
                {
                    Name = displayName,
                    Slug = pendingProject,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Status = "active"
                });
                await _db.SaveChangesAsync();
                _memory.SetActiveProject(pendingProject);
                projectCreated = pendingProject;
                _memory.ClearPendingProject();
            }

            var focusMatch = Regex.Match(cleanedMessage, @"([\w-]+(?:_[\w-]+)*)\s*(?:projesi|projesine|proje|projeye|focus)?\s*(?:ne|a|e|ye|ya|)?\s*(?:odaklan|bağlan|baglan|geç|gec)", RegexOptions.IgnoreCase);
            string? focusError = null;
            if (projectCreated == null && focusMatch.Success)
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
            else if (focusMatch.Success && projectCreated != null)
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

            var isCodeChange = IsExplicitCodeCommand(request.Message) && !isCorrection;

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

                if (result != null)
                {
                    _memory.StoreConversation(request.Message, request.AgentId, result.Content, request.Room, true,
                        execResult.Success ? execResult.Summary : execResult.Error ?? "basarisiz");
                }
            }
            else if (result != null)
            {
                _memory.StoreConversation(request.Message, request.AgentId, result.Content, request.Room, false);
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
