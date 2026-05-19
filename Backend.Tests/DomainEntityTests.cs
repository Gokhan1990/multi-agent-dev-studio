using Xunit;
using SaaSFast.Domain.Entities;

namespace Backend.Tests;

public class DomainEntityTests
{
    [Fact]
    public void Idea_DefaultValues()
    {
        var idea = new Idea();
        Assert.Equal(0, idea.Id);
        Assert.Equal(string.Empty, idea.Title);
        Assert.Equal(string.Empty, idea.Description);
        Assert.Null(idea.Source);
        Assert.Equal(0, idea.Status);
        Assert.True((DateTime.UtcNow - idea.CreatedAt).TotalSeconds < 5);
    }

    [Fact]
    public void Idea_CanSetProperties()
    {
        var idea = new Idea
        {
            Title = "Test Idea",
            Description = "A test idea description",
            Source = "user",
            Status = 1
        };
        Assert.Equal("Test Idea", idea.Title);
        Assert.Equal("A test idea description", idea.Description);
        Assert.Equal("user", idea.Source);
        Assert.Equal(1, idea.Status);
    }

    [Fact]
    public void Agent_DefaultValues()
    {
        var agent = new Agent();
        Assert.Equal(0, agent.Id);
        Assert.Equal(string.Empty, agent.Name);
        Assert.Equal(string.Empty, agent.Role);
        Assert.Equal(string.Empty, agent.Room);
        Assert.Equal(string.Empty, agent.VoiceId);
        Assert.True(agent.Active);
    }

    [Fact]
    public void Agent_CanSetProperties()
    {
        var agent = new Agent
        {
            Name = "Test Agent",
            Role = "Tester",
            Room = "strategy",
            VoiceId = "Google UK English Male",
            Active = false
        };
        Assert.Equal("Test Agent", agent.Name);
        Assert.Equal("Tester", agent.Role);
        Assert.Equal("strategy", agent.Room);
        Assert.Equal("Google UK English Male", agent.VoiceId);
        Assert.False(agent.Active);
    }

    [Fact]
    public void Room_DefaultValues()
    {
        var room = new Room();
        Assert.Equal(0, room.Id);
        Assert.Equal(string.Empty, room.Name);
        Assert.Equal(string.Empty, room.Type);
    }

    [Fact]
    public void ChatMessage_DefaultValues()
    {
        var msg = new ChatMessage();
        Assert.Equal(0, msg.Id);
        Assert.Equal(string.Empty, msg.Content);
        Assert.Equal(string.Empty, msg.Sender);
        Assert.Equal(string.Empty, msg.Room);
        Assert.Equal(string.Empty, msg.VoiceId);
        Assert.False(msg.IsUser);
        Assert.True((DateTime.UtcNow - msg.Timestamp).TotalSeconds < 5);
    }

    [Fact]
    public void ChatMessage_CanSetProperties()
    {
        var msg = new ChatMessage
        {
            Content = "Hello",
            Sender = "User",
            Room = "strategy",
            VoiceId = "voice1",
            IsUser = true
        };
        Assert.Equal("Hello", msg.Content);
        Assert.Equal("User", msg.Sender);
        Assert.Equal("strategy", msg.Room);
        Assert.Equal("voice1", msg.VoiceId);
        Assert.True(msg.IsUser);
    }
}
