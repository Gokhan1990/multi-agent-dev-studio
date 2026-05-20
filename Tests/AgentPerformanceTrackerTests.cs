using Xunit;
using SaaSFast.Application.Services;

namespace Tests;

public class AgentPerformanceTrackerTests
{
    private readonly AgentPerformanceTracker _tracker = new();

    [Fact]
    public void InitialStats_AreZero()
    {
        var stats = _tracker.GetStats("backend");
        Assert.Equal(0, stats.TotalTasks);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Equal(0, stats.FailureCount);
    }

    [Fact]
    public void RecordSuccess_IncrementsCount()
    {
        _tracker.RecordSuccess("backend", "api");

        var stats = _tracker.GetStats("backend");
        Assert.Equal(1, stats.TotalTasks);
        Assert.Equal(1, stats.SuccessCount);
        Assert.Equal(0, stats.FailureCount);
    }

    [Fact]
    public void RecordFailure_IncrementsFailure()
    {
        _tracker.RecordFailure("backend", "api");

        var stats = _tracker.GetStats("backend");
        Assert.Equal(1, stats.TotalTasks);
        Assert.Equal(0, stats.SuccessCount);
        Assert.Equal(1, stats.FailureCount);
    }

    [Fact]
    public void SuccessRate_CalculatesCorrectly()
    {
        _tracker.RecordSuccess("backend", "task1");
        _tracker.RecordSuccess("backend", "task2");
        _tracker.RecordFailure("backend", "task3");

        var stats = _tracker.GetStats("backend");
        Assert.Equal(3, stats.TotalTasks);
        Assert.Equal(2, stats.SuccessCount);
        Assert.Equal(66.67, stats.SuccessRate, 1);
    }

    [Fact]
    public void GetBestAgentForTask_ReturnsCandidateFromRoom()
    {
        _tracker.RecordSuccess("backend", "api");

        var best = _tracker.GetBestAgentForTask("api", "engineering");
        Assert.Equal("backend", best);
    }

    [Fact]
    public void GetBestAgentForTask_FallsBackToCeo_WhenNoCandidates()
    {
        var best = _tracker.GetBestAgentForTask("api", "nonexistent");
        Assert.Equal("ceo", best);
    }

    [Fact]
    public void GetBestAgentForTask_PrefersExperiencedAgent()
    {
        _tracker.RecordSuccess("backend", "api");
        _tracker.RecordSuccess("backend", "api");
        _tracker.RecordSuccess("frontend", "ui");

        var best = _tracker.GetBestAgentForTask("api", "engineering");
        Assert.Equal("backend", best);
    }

    [Fact]
    public void RecordSuccess_UpdatesLastActive()
    {
        _tracker.RecordSuccess("backend", "api");

        var stats = _tracker.GetStats("backend");
        Assert.True((DateTime.UtcNow - stats.LastActive).TotalSeconds < 5);
    }

    [Fact]
    public void RecordSuccess_UpdatesRecentTaskTypes()
    {
        _tracker.RecordSuccess("backend", "api");
        _tracker.RecordSuccess("backend", "frontend");

        var stats = _tracker.GetStats("backend");
        Assert.Contains("api", stats.RecentTaskTypes);
        Assert.Contains("frontend", stats.RecentTaskTypes);
    }

    [Fact]
    public void GetAllStatsSummary_ReturnsEmpty_WhenNoTasks()
    {
        var summary = _tracker.GetAllStatsSummary();
        Assert.Equal("Henuz gorev yok", summary);
    }

    [Fact]
    public void GetAllStatsSummary_IncludesStats()
    {
        _tracker.RecordSuccess("backend", "api");

        var summary = _tracker.GetAllStatsSummary();
        Assert.Contains("backend", summary);
        Assert.Contains("1/1", summary);
    }
}
