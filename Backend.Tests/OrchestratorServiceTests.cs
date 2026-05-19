using Xunit;
using SaaSFast.Application.Services;

namespace Backend.Tests;

public class OrchestratorServiceTests
{
    private readonly OrchestratorService _orchestrator;

    public OrchestratorServiceTests()
    {
        var voice = new VoiceService();
        var router = new AgentRouterService(voice);
        _orchestrator = new OrchestratorService(router, voice);
    }

    [Fact]
    public void ProcessMessage_ReturnsResponseWithUserMessage()
    {
        var result = _orchestrator.ProcessMessage("merhaba", "strategy");
        Assert.Equal("merhaba", result.UserMessage);
        Assert.False(string.IsNullOrWhiteSpace(result.Content));
    }

    [Fact]
    public void ProcessMessage_TimestampIsRecent()
    {
        var result = _orchestrator.ProcessMessage("merhaba", "strategy");
        Assert.True((DateTime.UtcNow - result.Timestamp).TotalSeconds < 5);
    }

    [Fact]
    public void ProcessMessage_ReturnsResponseForEngineering()
    {
        var result = _orchestrator.ProcessMessage("yeni bir özellik", "engineering");
        Assert.Equal("engineering", result.Room);
        Assert.Contains(result.AgentId, new[] { "backend", "frontend", "qa", "devops" });
    }

    [Fact]
    public void ProcessMessage_ReturnsResponseForStrategy()
    {
        var result = _orchestrator.ProcessMessage("yeni bir özellik", "strategy");
        Assert.Equal("strategy", result.Room);
        Assert.Contains(result.AgentId, new[] { "ceo", "product", "research", "architect" });
    }

    [Fact]
    public void ProcessMessage_AgentNameIsNotEmpty()
    {
        var result = _orchestrator.ProcessMessage("merhaba", "strategy");
        Assert.False(string.IsNullOrWhiteSpace(result.AgentName));
    }
}
