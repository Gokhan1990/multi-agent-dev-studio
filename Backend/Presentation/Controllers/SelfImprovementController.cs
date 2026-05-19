using Microsoft.AspNetCore.Mvc;
using SaaSFast.Application.Services;

namespace SaaSFast.Presentation.Controllers;

[ApiController]
[Route("api/self-improve")]
public class SelfImprovementController : ControllerBase
{
    private readonly SelfImprovementService _self;
    private readonly AgentPerformanceTracker _performance;

    public SelfImprovementController(SelfImprovementService self, AgentPerformanceTracker performance)
    {
        _self = self;
        _performance = performance;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan()
    {
        var report = await _self.AnalyzeAsync();
        return Ok(report);
    }

    [HttpGet("suggestions")]
    public IActionResult GetSuggestions([FromQuery] string? status = null)
    {
        return Ok(_self.GetSuggestions(status));
    }

    [HttpPost("apply/{id}")]
    public async Task<IActionResult> Apply(string id)
    {
        var result = await _self.ApplySuggestionAsync(id);
        if (result == null)
            return NotFound(new { error = "Suggestion not found or already applied" });
        return Ok(result);
    }

    [HttpPost("dismiss/{id}")]
    public IActionResult Dismiss(string id)
    {
        _self.DismissSuggestion(id);
        return Ok(new { status = "dismissed" });
    }

    [HttpPost("auto")]
    public IActionResult ToggleAuto([FromBody] AutoModeRequest request)
    {
        _self.SetAutoMode(request.Enabled);
        return Ok(new { autoMode = request.Enabled });
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var allStats = _performance.GetAllStatsSummary();
        return Ok(new
        {
            autoMode = _self.IsAutoMode,
            lastScan = _self.LastScan,
            pendingCount = _self.GetSuggestions("pending").Count,
            appliedCount = _self.GetSuggestions("applied").Count,
            performance = allStats
        });
    }

    [HttpGet("performance")]
    public IActionResult Performance()
    {
        var stats = AgentRegistry.All.Select(a =>
        {
            var s = _performance.GetStats(a.Id);
            return new
            {
                agentId = a.Id,
                agentName = a.Name,
                room = a.Room,
                totalTasks = s.TotalTasks,
                successCount = s.SuccessCount,
                failureCount = s.FailureCount,
                successRate = $"{s.SuccessRate:F0}%",
                lastActive = s.LastActive,
                lastTaskType = s.LastTaskType
            };
        }).ToList();

        return Ok(stats);
    }
}

public class AutoModeRequest
{
    public bool Enabled { get; set; }
}
