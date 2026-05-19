using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSFast.Application.Services;
using SaaSFast.Infrastructure.Data;

namespace SaaSFast.Presentation.Controllers
{
    [ApiController]
    [Route("api/agents")]
    public class AgentController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly VoiceService _voice;

        public AgentController(AppDbContext db, VoiceService voice)
        {
            _db = db;
            _voice = voice;
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            var agents = AgentRegistry.All.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                room = a.Room,
                voiceId = a.VoiceId
            });
            return Ok(agents);
        }

        [HttpGet("voices")]
        public IActionResult GetVoices()
        {
            return Ok(_voice.GetAllVoices());
        }
    }
}

// AgentController updated by opencode AI.
