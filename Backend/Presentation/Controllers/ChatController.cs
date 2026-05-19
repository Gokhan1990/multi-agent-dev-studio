using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSFast.Application.Services;
using SaaSFast.Domain.Entities;
using SaaSFast.Infrastructure.Data;

namespace SaaSFast.Presentation.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly OrchestratorService _orchestrator;

        public ChatController(AppDbContext db, OrchestratorService orchestrator)
        {
            _db = db;
            _orchestrator = orchestrator;
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] ChatRequest request)
        {
            var userMsg = new ChatMessage
            {
                Content = request.Message,
                Sender = "User",
                Room = request.Room ?? "strategy",
                IsUser = true,
                Timestamp = DateTime.UtcNow
            };
            _db.ChatMessages.Add(userMsg);

            var result = _orchestrator.ProcessMessage(request.Message, request.Room ?? "strategy");

            var agentMsg = new ChatMessage
            {
                Content = result.Content,
                Sender = result.AgentName,
                Room = result.Room,
                VoiceId = result.VoiceId,
                IsUser = false,
                Timestamp = result.Timestamp
            };
            _db.ChatMessages.Add(agentMsg);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                userMessage = new { content = userMsg.Content, sender = userMsg.Sender, room = userMsg.Room, timestamp = userMsg.Timestamp },
                agentResponse = new
                {
                    content = agentMsg.Content,
                    agentId = result.AgentId,
                    agentName = result.AgentName,
                    room = result.Room,
                    voiceId = result.VoiceId,
                    timestamp = result.Timestamp
                }
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> History([FromQuery] string room = "strategy")
        {
            var messages = await _db.ChatMessages
                .Where(m => m.Room == room)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
            return Ok(messages);
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = "";
        public string? Room { get; set; }
    }
}
