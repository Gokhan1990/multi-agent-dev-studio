using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SaaSFast.Application.Services;

namespace SaaSFast.Presentation.Controllers
{
    [ApiController]
    [Route("api/command")]
    public class CommandController : ControllerBase
    {
        private readonly CommandQueueService _queue;
        private readonly CodeExecutorService _executor;
        private readonly IConfiguration _config;
        private readonly AgentMemoryService _memory;

        public CommandController(CommandQueueService queue, CodeExecutorService executor, IConfiguration config, AgentMemoryService memory)
        {
            _queue = queue;
            _executor = executor;
            _config = config;
            _memory = memory;
        }

        [HttpGet("ping")]
        public IActionResult Ping() => Ok(new { status = "ok", time = DateTime.UtcNow });

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] CommandRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(new { error = "Text is required" });

            var cmd = _queue.Enqueue(request.Text);
            cmd.ActiveAgentId = DetectAgent(cmd.Text);

            var execResult = await _executor.ExecuteAsync(cmd);

            return Ok(new
            {
                cmd.Id,
                cmd.Text,
                cmd.TargetFile,
                cmd.ChangeType,
                cmd.Status,
                cmd.CreatedAt,
                Result = new
                {
                    execResult.Success,
                    execResult.Summary,
                    Diff = execResult.Diff?.DiffText,
                    execResult.Error
                }
            });
        }

        [HttpPost("rebuild")]
        public async Task<IActionResult> RebuildFrontend()
        {
            try
            {
                var sourceRoot = _config.GetValue<string>("SourceRoot") ?? "/source";
                var frontendDir = Path.Combine(sourceRoot, "frontend");

                if (!Directory.Exists(frontendDir))
                    return BadRequest(new { error = "Frontend dizini bulunamadi" });

                var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "docker",
                        Arguments = "compose up -d --build frontend",
                        WorkingDirectory = Path.Combine(sourceRoot, "Infrastructure"),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                proc.Start();
                var output = await proc.StandardOutput.ReadToEndAsync();
                var error = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                return Ok(new { success = proc.ExitCode == 0, output, error });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        [HttpGet("pending")]
        public IActionResult GetPending()
        {
            return Ok(_queue.GetPending());
        }

        [HttpGet("activity")]
        public IActionResult GetActivity()
        {
            var active = _queue.GetActive();
            var recent = _queue.GetRecent(10);
            return Ok(new { active, recent });
        }

        [HttpGet("feed")]
        public IActionResult GetFeed([FromQuery] int count = 100)
        {
            return Ok(_queue.GetFeed(count));
        }

        [HttpGet("conversations")]
        public IActionResult GetConversations([FromQuery] int count = 20)
        {
            return Ok(_memory.GetRecentConversations(count));
        }

        private static string? DetectAgent(string text)
        {
            var lower = text.ToLowerInvariant();
            if (lower.Contains("bora") || lower.Contains("backend") || lower.Contains("api"))
                return "bora";
            if (lower.Contains("deniz") || lower.Contains("frontend") || lower.Contains("ui") || lower.Contains("tasar") || lower.Contains("renk") || lower.Contains("css"))
                return "deniz";
            if (lower.Contains("sibel") || lower.Contains("devops") || lower.Contains("deploy"))
                return "sibel";
            if (lower.Contains("cem") || lower.Contains("test") || lower.Contains("qa"))
                return "cem";
            if (lower.Contains("zeynep") || lower.Contains("mimar") || lower.Contains("architect"))
                return "zeynep";
            if (lower.Contains("elif") || lower.Contains("ürün") || lower.Contains("product"))
                return "elif";
            if (lower.Contains("kerem") || lower.Contains("araştırma") || lower.Contains("research"))
                return "kerem";
            if (lower.Contains("atilla") || lower.Contains("ceo"))
                return "atilla";
            return "bora";
        }
    }

    public class CommandRequest
    {
        public string Text { get; set; } = "";
    }
}
