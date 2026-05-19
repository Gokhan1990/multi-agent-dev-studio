using Xunit;
using SaaSFast.Application.Services;

namespace Backend.Tests;

public class AgentRegistryTests
{
    [Fact]
    public void All_ReturnsAllEightAgents()
    {
        var agents = AgentRegistry.All;
        Assert.Equal(8, agents.Count);
    }

    [Fact]
    public void All_ContainsExpectedAgentIds()
    {
        var ids = AgentRegistry.All.Select(a => a.Id).ToHashSet();
        Assert.Contains("ceo", ids);
        Assert.Contains("product", ids);
        Assert.Contains("research", ids);
        Assert.Contains("architect", ids);
        Assert.Contains("backend", ids);
        Assert.Contains("frontend", ids);
        Assert.Contains("qa", ids);
        Assert.Contains("devops", ids);
    }

    [Fact]
    public void All_AgentsHaveRoomAssignment()
    {
        var strategy = AgentRegistry.All.Where(a => a.Room == "strategy").ToList();
        var engineering = AgentRegistry.All.Where(a => a.Room == "engineering").ToList();

        Assert.Equal(4, strategy.Count);
        Assert.Equal(4, engineering.Count);
    }

    [Fact]
    public void All_EveryAgentHasNonEmptyVoiceId()
    {
        Assert.All(AgentRegistry.All, a => Assert.False(string.IsNullOrWhiteSpace(a.VoiceId)));
    }

    [Fact]
    public void GetById_ReturnsCorrectAgent()
    {
        var agent = AgentRegistry.GetById("backend");
        Assert.Equal("backend", agent.Id);
        Assert.Equal("Backend Developer", agent.Name);
    }

    [Fact]
    public void GetById_ReturnsCeoForUnknownId()
    {
        var agent = AgentRegistry.GetById("nonexistent");
        Assert.Equal("ceo", agent.Id);
    }
}
