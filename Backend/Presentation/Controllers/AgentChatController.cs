using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SaaSFast.Application.Services;

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

        private static readonly string[] CodeVerbs = {
            "değiştir", "güncelle", "düzelt", "yenile", "ayarla",
            "yap", "ekle", "sil", "kaldır", "çıkar",
            "büyüt", "küçült", "taşı",
            "yaz"
        };

        private static bool IsExplicitCodeCommand(string message)
        {
            var lower = message.ToLowerInvariant();
            return CodeVerbs.Any(v => Regex.IsMatch(lower, $@"\b{Regex.Escape(v)}\b"));
        }

        public AgentChatController(AiService ai, VoiceService voice, CommandQueueService queue, CodeExecutorService executor, AgentMemoryService memory)
        {
            _ai = ai;
            _voice = voice;
            _queue = queue;
            _executor = executor;
            _memory = memory;
        }

        [HttpPost("ask")]
        public async Task<IActionResult> Ask([FromBody] AiRequest request)
        {
            var result = await _ai.AskAsync(request);
            var response = new Dictionary<string, object>
            {
                ["content"] = result?.Content ?? "",
                ["agentId"] = result?.AgentId ?? "",
                ["agentName"] = result?.AgentName ?? "",
                ["voiceId"] = result?.VoiceId ?? "",
                ["room"] = result?.Room ?? ""
            };

            var isCodeChange = IsExplicitCodeCommand(request.Message);
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
    }
}
