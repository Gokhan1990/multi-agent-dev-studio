using Xunit;
using SaaSFast.Application.Services;

namespace Backend.Tests;

public class VoiceServiceTests
{
    private readonly VoiceService _voice = new();

    [Fact]
    public void GetVoiceId_ReturnsVoiceIdForExistingAgent()
    {
        var voiceId = _voice.GetVoiceId("backend");
        Assert.Equal("Google UK English Male", voiceId);
    }

    [Fact]
    public void GetVoiceId_ReturnsDefaultForUnknownAgent()
    {
        var voiceId = _voice.GetVoiceId("unknown");
        Assert.Equal("Google UK English Male", voiceId);
    }

    [Fact]
    public void GetAllVoices_ReturnsAllAgentVoices()
    {
        var voices = _voice.GetAllVoices();
        Assert.Equal(8, voices.Count);
        Assert.True(voices.ContainsKey("ceo"));
        Assert.True(voices.ContainsKey("product"));
        Assert.True(voices.ContainsKey("backend"));
    }
}
