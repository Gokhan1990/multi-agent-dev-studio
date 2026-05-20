using Xunit;
using SaaSFast.Application.Services;

namespace Tests;

public class AgentAbilityServiceTests
{
    private readonly AgentAbilityService _service = new();

    [Fact]
    public void GetAll_ReturnsAbilities()
    {
        var abilities = _service.GetAll();
        Assert.NotEmpty(abilities);
    }

    [Fact]
    public void GetAll_EachAbilityHasRequiredFields()
    {
        var abilities = _service.GetAll();
        foreach (var a in abilities)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Id));
            Assert.False(string.IsNullOrWhiteSpace(a.Name));
            Assert.False(string.IsNullOrWhiteSpace(a.Description));
            Assert.False(string.IsNullOrWhiteSpace(a.AgentId));
        }
    }

    [Fact]
    public void GetByAgent_ReturnsAbilitiesForAgent()
    {
        var backendAbilities = _service.GetByAgent("backend");
        Assert.NotEmpty(backendAbilities);
        Assert.All(backendAbilities, a => Assert.Equal("backend", a.AgentId));
    }

    [Fact]
    public void GetByAgent_ReturnsEmpty_ForUnknownAgent()
    {
        var abilities = _service.GetByAgent("nonexistent");
        Assert.Empty(abilities);
    }

    [Fact]
    public void GetByAgent_CeoHasSixAbilities()
    {
        var abilities = _service.GetByAgent("ceo");
        Assert.Equal(6, abilities.Count);
    }

    [Fact]
    public void GetByAgent_ProductHasFiveAbilities()
    {
        var abilities = _service.GetByAgent("product");
        Assert.Equal(5, abilities.Count);
    }

    [Fact]
    public void GetByAgent_ResearchHasSixAbilities()
    {
        var abilities = _service.GetByAgent("research");
        Assert.Equal(6, abilities.Count);
    }

    [Fact]
    public void GetByAgent_ArchitectHasFiveAbilities()
    {
        var abilities = _service.GetByAgent("architect");
        Assert.Equal(5, abilities.Count);
    }

    [Fact]
    public void GetByAgent_BackendHasSixAbilities()
    {
        var abilities = _service.GetByAgent("backend");
        Assert.Equal(6, abilities.Count);
    }

    [Fact]
    public void GetByAgent_FrontendHasSixAbilities()
    {
        var abilities = _service.GetByAgent("frontend");
        Assert.Equal(6, abilities.Count);
    }

    [Fact]
    public void GetByAgent_QaHasEightAbilities()
    {
        var abilities = _service.GetByAgent("qa");
        Assert.Equal(8, abilities.Count);
    }

    [Fact]
    public void GetByAgent_DevopsHasSixAbilities()
    {
        var abilities = _service.GetByAgent("devops");
        Assert.Equal(6, abilities.Count);
    }

    [Fact]
    public void GetAll_HasUniqueIds()
    {
        var abilities = _service.GetAll();
        var ids = abilities.Select(a => a.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
