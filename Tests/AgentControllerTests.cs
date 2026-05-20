using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSFast.Application.Services;
using SaaSFast.Infrastructure.Data;
using SaaSFast.Presentation.Controllers;

namespace Tests;

public class AgentControllerTests
{
    [Fact]
    public void GetAll_ReturnsAllAgents()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("AgentGetAll")
            .Options;
        using var db = new AppDbContext(options);
        var voice = new VoiceService();
        var controller = new AgentController(db, voice);

        var result = controller.GetAll() as OkObjectResult;

        Assert.NotNull(result);
        var agents = result.Value as IEnumerable<object>;
        Assert.NotNull(agents);
        Assert.Equal(8, agents.Cast<object>().Count());
    }

    [Fact]
    public void GetVoices_ReturnsAllVoices()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("AgentVoices")
            .Options;
        using var db = new AppDbContext(options);
        var voice = new VoiceService();
        var controller = new AgentController(db, voice);

        var result = controller.GetVoices() as OkObjectResult;

        Assert.NotNull(result);
        var voices = result.Value as Dictionary<string, string>;
        Assert.NotNull(voices);
        Assert.Equal(8, voices.Count);
        Assert.True(voices.ContainsKey("ceo"));
        Assert.True(voices.ContainsKey("backend"));
    }
}
