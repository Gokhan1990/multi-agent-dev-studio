using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using SaaSFast.Application.Services;
using SaaSFast.Infrastructure.Data;
using SaaSFast.Presentation.Controllers;

namespace Tests;

public class AgentChatControllerTests
{
    private static AgentChatController CreateController()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceRoot"] = "/tmp/test",
                ["CommandQueuePath"] = "/tmp/test/queue"
            })
            .Build();

        var opencode = new OpencodeService(config);
        var memory = new AgentMemoryService(config);
        var perf = new AgentPerformanceTracker();
        var training = new AgentTrainingService(new AgentAbilityService());
        var ai = new AiService(opencode, memory, perf, training);
        var voice = new VoiceService();
        var queue = new CommandQueueService(config);
        var executor = new CodeExecutorService(config, queue, new HttpClient(), memory, perf, new CodeReviewService(), opencode);

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("TestDb_" + Guid.NewGuid())
            .Options;
        var db = new AppDbContext(dbOptions);

        return new AgentChatController(ai, voice, queue, executor, memory, db);
    }

    [Fact]
    public void Voices_ReturnsAllAgentVoices()
    {
        var controller = CreateController();

        var result = controller.Voices() as OkObjectResult;

        Assert.NotNull(result);
        var voices = result.Value as Dictionary<string, string>;
        Assert.NotNull(voices);
        Assert.Equal(8, voices.Count);
        Assert.True(voices.ContainsKey("ceo"));
        Assert.True(voices.ContainsKey("backend"));
    }

    [Fact]
    public void Voices_ContainsVoiceForEachAgent()
    {
        var controller = CreateController();

        var result = controller.Voices() as OkObjectResult;

        Assert.NotNull(result);
        var voices = result.Value as Dictionary<string, string>;
        Assert.NotNull(voices);
        Assert.All(AgentRegistry.All, a => Assert.True(voices.ContainsKey(a.Id)));
        Assert.All(AgentRegistry.All, a => Assert.Equal(a.VoiceId, voices[a.Id]));
    }

    [Fact]
    public async Task Chime_WithDefaultLanguage_ReturnsResult()
    {
        var controller = CreateController();

        var result = await controller.Chime(new ChimeRequest { Room = "strategy" }) as OkObjectResult;

        Assert.NotNull(result);
        var dict = result.Value;
        var agentIdProp = dict.GetType().GetProperty("agentId");
        Assert.NotNull(agentIdProp);
    }

    [Fact]
    public async Task Ask_WithSimpleMessage_ReturnsResponse()
    {
        var controller = CreateController();

        var result = await controller.Ask(new AiRequest
        {
            Message = "merhaba",
            AgentId = "backend",
            Room = "engineering"
        }) as OkObjectResult;

        Assert.NotNull(result);
        var dict = result.Value as Dictionary<string, object>;
        Assert.NotNull(dict);
        Assert.True(dict.ContainsKey("content"));
        Assert.True(dict.ContainsKey("agentId"));
        Assert.True(dict.ContainsKey("agentName"));
        Assert.True(dict.ContainsKey("voiceId"));
        Assert.True(dict.ContainsKey("room"));
    }
}
