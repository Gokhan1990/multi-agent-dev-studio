using Microsoft.AspNetCore.Mvc;
using SaaSFast.Application.Services;

namespace SaaSFast.Presentation.Controllers;

[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly AgentFeedbackService _feedback;

    public FeedbackController(AgentFeedbackService feedback)
    {
        _feedback = feedback;
    }

    [HttpGet("{agentId}")]
    public IActionResult GetLessons(string agentId)
    {
        return Ok(_feedback.GetAgentLessons(agentId));
    }

    [HttpDelete("{agentId}")]
    public IActionResult Forget(string agentId)
    {
        _feedback.ForgetAgent(agentId);
        return Ok(new { status = "forgotten", agentId });
    }
}
