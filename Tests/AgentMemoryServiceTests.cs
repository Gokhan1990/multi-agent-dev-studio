using Xunit;
using Microsoft.Extensions.Configuration;
using SaaSFast.Application.Services;

namespace Tests;

public class AgentMemoryServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AgentMemoryService _memory;

    public AgentMemoryServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SaaSFast_MemoryTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceRoot"] = _tempDir
            })
            .Build();

        _memory = new AgentMemoryService(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void StoreConversation_AddsEntry()
    {
        _memory.StoreConversation("merhaba", "backend", "merhaba ben Bora", "engineering", false);

        var recent = _memory.GetRecentConversations(10);
        Assert.Single(recent);
        Assert.Equal("merhaba", recent[0].UserMessage);
        Assert.Equal("backend", recent[0].AgentId);
        Assert.Equal("engineering", recent[0].Room);
    }

    [Fact]
    public void StoreConversation_WithCodeChange()
    {
        _memory.StoreConversation("api ekle", "backend", "ekledim", "engineering", true, "code result");

        var recent = _memory.GetRecentConversations(10);
        Assert.True(recent[0].HadCodeChange);
        Assert.Equal("code result", recent[0].CodeResult);
    }

    [Fact]
    public void GetRecentConversations_ReturnsLatestFirst()
    {
        _memory.StoreConversation("first", "agent1", "resp1", "room1", false);
        _memory.StoreConversation("second", "agent2", "resp2", "room2", false);

        var recent = _memory.GetRecentConversations(2);
        Assert.Equal(2, recent.Count);
        Assert.Equal("second", recent[0].UserMessage);
    }

    [Fact]
    public void GetRecentConversations_RespectsCount()
    {
        for (int i = 0; i < 5; i++)
            _memory.StoreConversation($"msg{i}", "agent", "resp", "room", false);

        var recent = _memory.GetRecentConversations(3);
        Assert.Equal(3, recent.Count);
    }

    [Fact]
    public void StoreEpisodic_AddsMemoryEntry()
    {
        _memory.StoreEpisodic("backend", "api ekle", "code_change", "basarili", "completed");

        var context = _memory.GetRecentContext("backend", 10);
        Assert.Contains("api ekle", context);
        Assert.Contains("basarili", context);
    }

    [Fact]
    public void GetConversationContext_ReturnsRoomSpecific()
    {
        _memory.StoreConversation("strategy msg", "ceo", "ok", "strategy", false);
        _memory.StoreConversation("engineering msg", "backend", "ok", "engineering", false);

        var context = _memory.GetConversationContext("backend", "strategy", 5);
        Assert.Contains("strategy msg", context);
        Assert.DoesNotContain("engineering msg", context);
    }

    [Fact]
    public void GetConversationContext_ReturnsEmpty_WhenNoHistory()
    {
        var context = _memory.GetConversationContext("backend", "nonexistent", 5);
        Assert.Equal("", context);
    }

    [Fact]
    public void GetAgentHistoryContext_ReturnsEmpty_WhenNoHistory()
    {
        var context = _memory.GetAgentHistoryContext("nonexistent", 3);
        Assert.Equal("", context);
    }

    [Fact]
    public void GetRecentContext_ReturnsEmpty_WhenNoEpisodic()
    {
        var context = _memory.GetRecentContext("backend", 3);
        Assert.Equal("", context);
    }

    [Fact]
    public void StoreEpisodic_TruncatesLongFields()
    {
        var longTask = new string('x', 200);
        var longResult = new string('y', 300);

        _memory.StoreEpisodic("backend", longTask, "action", longResult, "completed");

        var context = _memory.GetRecentContext("backend", 10);
        Assert.True(context.Length < 600);
    }

    [Fact]
    public void GetPeerReviewContext_ReturnsPeerSuccesses()
    {
        _memory.StoreEpisodic("backend", "api yap", "code_change", "basarili", "completed");
        _memory.StoreEpisodic("frontend", "ui yap", "code_change", "basarili", "completed");

        var context = _memory.GetPeerReviewContext("frontend", 5);
        Assert.Contains("backend", context);
    }

    [Fact]
    public void GetPeerReviewContext_ExcludesOwnEntries()
    {
        _memory.StoreEpisodic("backend", "api yap", "code_change", "basarili", "completed");

        var context = _memory.GetPeerReviewContext("backend", 5);
        Assert.DoesNotContain("backend", context);
    }
}
