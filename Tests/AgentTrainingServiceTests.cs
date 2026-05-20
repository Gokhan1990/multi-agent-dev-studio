using Xunit;
using SaaSFast.Application.Services;

namespace Tests;

public class AgentTrainingServiceTests
{
    private readonly AgentTrainingService _service = new(new AgentAbilityService());

    [Fact]
    public void GetTrainingContext_ReturnsContext_ForKnownAgent()
    {
        var context = _service.GetTrainingContext("backend", true);
        Assert.Contains("YETENEK EĞİTİMİ", context);
        Assert.Contains(".NET Projesi İskeleti Oluşturma", context);
    }

    [Fact]
    public void GetTrainingContext_ReturnsEmpty_ForUnknownAgent()
    {
        var context = _service.GetTrainingContext("nonexistent", true);
        Assert.Equal("", context);
    }

    [Fact]
    public void GetTrainingContext_ReturnsEnglish_WhenTrFalse()
    {
        var context = _service.GetTrainingContext("backend", false);
        Assert.Contains("ABILITY TRAINING", context);
    }

    [Fact]
    public void GetAbilityExamplePrompt_ReturnsPrompt_ForKnownAgent()
    {
        var prompt = _service.GetAbilityExamplePrompt("ceo", true);
        Assert.NotEqual("", prompt);
        Assert.Contains("Örnek", prompt);
    }

    [Fact]
    public void GetAbilityExamplePrompt_ReturnsEmpty_ForUnknownAgent()
    {
        var prompt = _service.GetAbilityExamplePrompt("nonexistent", true);
        Assert.Equal("", prompt);
    }

    [Fact]
    public void GetAbilityExamplePrompt_ReturnsEnglish_WhenTrFalse()
    {
        var prompt = _service.GetAbilityExamplePrompt("ceo", false);
        Assert.Contains("Example", prompt);
    }

    [Fact]
    public void GetAbilityExamplePrompt_AllKnownAgents()
    {
        var ids = new[] { "ceo", "product", "research", "architect", "backend", "frontend", "qa", "devops" };
        foreach (var id in ids)
        {
            var prompt = _service.GetAbilityExamplePrompt(id, true);
            Assert.NotEqual("", prompt);
        }
    }

    [Fact]
    public void GetTrainingContext_ContainsUsageNote()
    {
        var context = _service.GetTrainingContext("backend", true);
        Assert.Contains("Kullanım", context);
    }
}
