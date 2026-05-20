using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaaSFast.Application.Services;
using SaaSFast.Domain.Entities;
using SaaSFast.Infrastructure.Data;
using SaaSFast.Presentation.Controllers;

namespace Tests;

public class ChatControllerTests
{
    private static AppDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task History_ReturnsEmpty_WhenNoMessages()
    {
        using var db = CreateDbContext("ChatHistoryEmpty");
        var router = new AgentRouterService(new VoiceService());
        var orchestrator = new OrchestratorService(router, new VoiceService());
        var controller = new ChatController(db, orchestrator);

        var result = await controller.History("strategy") as OkObjectResult;

        Assert.NotNull(result);
        var messages = result.Value as List<ChatMessage>;
        Assert.NotNull(messages);
        Assert.Empty(messages);
    }

    [Fact]
    public async Task Send_ReturnsResponseWithUserAndAgentMessages()
    {
        using var db = CreateDbContext("ChatSend");
        var router = new AgentRouterService(new VoiceService());
        var orchestrator = new OrchestratorService(router, new VoiceService());
        var controller = new ChatController(db, orchestrator);

        var result = await controller.Send(new ChatRequest { Message = "merhaba", Room = "strategy" }) as OkObjectResult;

        Assert.NotNull(result);
        var response = result.Value;
        var userProp = response.GetType().GetProperty("userMessage");
        var agentProp = response.GetType().GetProperty("agentResponse");
        Assert.NotNull(userProp);
        Assert.NotNull(agentProp);

        var userMsg = userProp.GetValue(response);
        var agentResp = agentProp.GetValue(response);
        Assert.NotNull(userMsg);
        Assert.NotNull(agentResp);

        var contentProp = userMsg.GetType().GetProperty("content");
        Assert.NotNull(contentProp);
        Assert.Equal("merhaba", contentProp.GetValue(userMsg)?.ToString());
    }

    [Fact]
    public async Task Send_SavesMessagesToDatabase()
    {
        using var db = CreateDbContext("ChatSendSave");
        var router = new AgentRouterService(new VoiceService());
        var orchestrator = new OrchestratorService(router, new VoiceService());
        var controller = new ChatController(db, orchestrator);

        await controller.Send(new ChatRequest { Message = "test mesaj", Room = "engineering" });

        var messages = await db.ChatMessages.ToListAsync();
        Assert.Equal(2, messages.Count);
        Assert.Contains(messages, m => m.IsUser);
        Assert.Contains(messages, m => !m.IsUser);
    }

    [Fact]
    public async Task History_FiltersByRoom()
    {
        using var db = CreateDbContext("ChatHistoryFilter");
        var router = new AgentRouterService(new VoiceService());
        var orchestrator = new OrchestratorService(router, new VoiceService());
        var controller = new ChatController(db, orchestrator);

        await controller.Send(new ChatRequest { Message = "strategy msg", Room = "strategy" });
        await controller.Send(new ChatRequest { Message = "engineering msg", Room = "engineering" });

        var history = await controller.History("strategy") as OkObjectResult;
        Assert.NotNull(history);
        var messages = history.Value as List<ChatMessage>;
        Assert.NotNull(messages);
        Assert.All(messages, m => Assert.Equal("strategy", m.Room));
    }

    [Fact]
    public async Task History_ReturnsMessagesOrderedByTimestamp()
    {
        using var db = CreateDbContext("ChatHistoryOrder");
        var router = new AgentRouterService(new VoiceService());
        var orchestrator = new OrchestratorService(router, new VoiceService());
        var controller = new ChatController(db, orchestrator);

        await controller.Send(new ChatRequest { Message = "first", Room = "strategy" });
        await Task.Delay(10);
        await controller.Send(new ChatRequest { Message = "second", Room = "strategy" });

        var history = await controller.History("strategy") as OkObjectResult;
        Assert.NotNull(history);
        var messages = history.Value as List<ChatMessage>;
        Assert.NotNull(messages);
        Assert.Equal(4, messages.Count);
        Assert.True(messages[0].Timestamp <= messages[1].Timestamp);
    }
}
