using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SaaSFast.Application.Services;
using SaaSFast.Presentation.Controllers;

namespace Tests;

public class CommandControllerTests
{
    private static (CommandQueueService, IConfiguration) CreateQueueAndConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceRoot"] = "/tmp/test",
                ["CommandQueuePath"] = "/tmp/test"
            })
            .Build();
        var queue = new CommandQueueService(config);
        return (queue, config);
    }

    [Fact]
    public void Ping_ReturnsOkWithStatus()
    {
        var (queue, config) = CreateQueueAndConfig();
        var executor = new Mock<CodeExecutorService>(config, queue, new HttpClient(), It.IsAny<AgentMemoryService>(), It.IsAny<AgentPerformanceTracker>(), It.IsAny<CodeReviewService>(), It.IsAny<OpencodeService>());
        var memory = new Mock<AgentMemoryService>(config);
        var controller = new CommandController(queue, executor.Object, config, memory.Object);

        var result = controller.Ping() as OkObjectResult;

        Assert.NotNull(result);
        var response = result.Value;
        var statusProp = response.GetType().GetProperty("status");
        Assert.NotNull(statusProp);
        Assert.Equal("ok", statusProp.GetValue(response)?.ToString());
    }

    [Fact]
    public void GetPending_ReturnsEmpty_WhenNoCommands()
    {
        var (queue, config) = CreateQueueAndConfig();
        var executor = new Mock<CodeExecutorService>(config, queue, new HttpClient(), It.IsAny<AgentMemoryService>(), It.IsAny<AgentPerformanceTracker>(), It.IsAny<CodeReviewService>(), It.IsAny<OpencodeService>());
        var memory = new Mock<AgentMemoryService>(config);
        var controller = new CommandController(queue, executor.Object, config, memory.Object);

        var result = controller.GetPending() as OkObjectResult;

        Assert.NotNull(result);
        var pending = result.Value as List<CodeCommand>;
        Assert.NotNull(pending);
        Assert.Empty(pending);
    }

    [Fact]
    public void GetFeed_ReturnsEmpty_WhenNoCommands()
    {
        var (queue, config) = CreateQueueAndConfig();
        var executor = new Mock<CodeExecutorService>(config, queue, new HttpClient(), It.IsAny<AgentMemoryService>(), It.IsAny<AgentPerformanceTracker>(), It.IsAny<CodeReviewService>(), It.IsAny<OpencodeService>());
        var memory = new Mock<AgentMemoryService>(config);
        var controller = new CommandController(queue, executor.Object, config, memory.Object);

        var result = controller.GetFeed(10) as OkObjectResult;

        Assert.NotNull(result);
        var feed = result.Value as List<CommandLog>;
        Assert.NotNull(feed);
        Assert.Empty(feed);
    }

    [Fact]
    public void GetActivity_ReturnsActiveAndRecent()
    {
        var (queue, config) = CreateQueueAndConfig();
        var executor = new Mock<CodeExecutorService>(config, queue, new HttpClient(), It.IsAny<AgentMemoryService>(), It.IsAny<AgentPerformanceTracker>(), It.IsAny<CodeReviewService>(), It.IsAny<OpencodeService>());
        var memory = new Mock<AgentMemoryService>(config);
        var controller = new CommandController(queue, executor.Object, config, memory.Object);

        var result = controller.GetActivity() as OkObjectResult;

        Assert.NotNull(result);
        var response = result.Value;
        var activeProp = response.GetType().GetProperty("active");
        var recentProp = response.GetType().GetProperty("recent");
        Assert.NotNull(activeProp);
        Assert.NotNull(recentProp);
    }
}
