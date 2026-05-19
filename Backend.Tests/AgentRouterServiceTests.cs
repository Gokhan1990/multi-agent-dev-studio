using Xunit;
using SaaSFast.Application.Services;

namespace Backend.Tests;

public class AgentRouterServiceTests
{
    private readonly AgentRouterService _router = new(new VoiceService());

    [Fact]
    public void Route_ReturnsStrategyAgentForStrategyRoom()
    {
        var response = _router.Route("strategy", "bir fikrim var");
        Assert.Equal("strategy", response.Room);
        Assert.Contains(response.AgentId, new[] { "ceo", "product", "research", "architect" });
    }

    [Fact]
    public void Route_ReturnsEngineeringAgentForEngineeringRoom()
    {
        var response = _router.Route("engineering", "bir fikrim var");
        Assert.Equal("engineering", response.Room);
        Assert.Contains(response.AgentId, new[] { "backend", "frontend", "qa", "devops" });
    }

    [Fact]
    public void Route_ReturnsStrategyAgentForUnknownRoom()
    {
        var response = _router.Route("unknown", "bir fikrim var");
        Assert.Contains(response.AgentId, new[] { "ceo", "product", "research", "architect" });
    }

    [Fact]
    public void Route_NonEmptyContent()
    {
        var response = _router.Route("strategy", "merhaba");
        Assert.False(string.IsNullOrWhiteSpace(response.Content));
    }

    [Theory]
    [InlineData("strategy", "merhaba")]
    [InlineData("strategy", "selam")]
    [InlineData("strategy", "hello")]
    [InlineData("engineering", "hi")]
    [InlineData("engineering", "günaydın")]
    [InlineData("engineering", "kolay gelsin")]
    public void Route_GreetingReturnsAgentResponse(string room, string greeting)
    {
        var response = _router.Route(room, greeting);
        Assert.False(string.IsNullOrWhiteSpace(response.Content));
        Assert.Equal(room == "engineering" ? "engineering" : "strategy", response.Room);
    }

    [Fact]
    public void Route_StrategyTopicResponse()
    {
        var response = _router.Route("strategy", "yeni bir uygulama yapalım");
        Assert.False(string.IsNullOrWhiteSpace(response.Content));
        Assert.Equal("strategy", response.Room);
    }

    [Fact]
    public void Route_EngineeringTopicResponse()
    {
        var response = _router.Route("engineering", "backend api ekle");
        Assert.False(string.IsNullOrWhiteSpace(response.Content));
        Assert.Equal("engineering", response.Room);
    }
}
