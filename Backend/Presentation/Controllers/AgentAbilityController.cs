using Microsoft.AspNetCore.Mvc;
using SaaSFast.Application.Services;

namespace SaaSFast.Presentation.Controllers;

[ApiController]
[Route("api/agent-abilities")]
public class AgentAbilityController : ControllerBase
{
    private readonly AgentAbilityService _abilityService;
    private readonly AgentTrainingService _training;
    private readonly CommandQueueService _queue;

    public AgentAbilityController(AgentAbilityService abilityService, AgentTrainingService training, CommandQueueService queue)
    {
        _abilityService = abilityService;
        _training = training;
        _queue = queue;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(_abilityService.GetAll());
    }

    [HttpGet("{agentId}")]
    public IActionResult GetByAgent(string agentId)
    {
        var abilities = _abilityService.GetByAgent(agentId);
        return Ok(new { agentId, abilities });
    }

    [HttpGet("{agentId}/training")]
    public IActionResult GetTraining(string agentId, [FromQuery] bool tr = true)
    {
        var context = _training.GetTrainingContext(agentId, tr);
        var examples = _training.GetAbilityExamplePrompt(agentId, tr);
        return Ok(new { agentId, training = context + examples });
    }

    public class ExecuteAbilityRequest
    {
        public string AbilityId { get; set; } = "";
        public string AgentId { get; set; } = "";
        public string Details { get; set; } = "";
    }

    [HttpPost("execute")]
    public async Task<IActionResult> Execute([FromBody] ExecuteAbilityRequest request)
    {
        var ability = _abilityService.GetAll().FirstOrDefault(a => a.Id == request.AbilityId && a.AgentId == request.AgentId);
        if (ability == null)
            return NotFound(new { error = "Ability not found" });

        var cmdText = $"[{ability.Name}] {request.Details}";
        var cmd = _queue.Enqueue(cmdText);
        cmd.ActiveAgentId = request.AgentId;
        _queue.AddLog(cmd.Id, request.AgentId, $"🎯 {ability.Name} yetenegi calistiriliyor: {request.Details}", "info");
        _queue.AddLog(cmd.Id, request.AgentId, $"   {ability.Description}", "info");

        return Ok(new { cmd.Id, cmd.Text, cmd.TargetFile, cmd.ChangeType, cmd.Status });
    }
}
