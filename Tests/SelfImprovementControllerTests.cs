using Xunit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SaaSFast.Application.Services;
using SaaSFast.Presentation.Controllers;

namespace Tests;

public class SelfImprovementControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SelfImprovementController _controller;
    private readonly AgentPerformanceTracker _perf;

    public SelfImprovementControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SaaSFast_SelfImprove_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "AgentMemory", "Improvements"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "queue"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SourceRoot"] = _tempDir,
                ["CommandQueuePath"] = Path.Combine(_tempDir, "queue")
            })
            .Build();

        var queue = new CommandQueueService(config);
        var memory = new AgentMemoryService(config);
        _perf = new AgentPerformanceTracker();
        var opencode = new OpencodeService(config);
        var executor = new CodeExecutorService(config, queue, new HttpClient(), memory, _perf, new CodeReviewService(), opencode);
        var self = new SelfImprovementService(config, queue, memory, _perf, opencode, executor);

        _controller = new SelfImprovementController(self, _perf);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetSuggestions_ReturnsEmpty_WhenNoSuggestions()
    {
        var result = _controller.GetSuggestions(null) as OkObjectResult;

        Assert.NotNull(result);
        var list = result.Value as List<ImprovementSuggestion>;
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Fact]
    public void GetSuggestions_ReturnsEmpty_WhenNoPending()
    {
        var result = _controller.GetSuggestions("pending") as OkObjectResult;

        Assert.NotNull(result);
        var list = result.Value as List<ImprovementSuggestion>;
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    [Fact]
    public async Task Apply_ReturnsNotFound_WhenSuggestionMissing()
    {
        var result = await _controller.Apply("nonexistent");

        var notFound = result as NotFoundObjectResult;
        Assert.NotNull(notFound);
    }

    [Fact]
    public void Dismiss_ReturnsOk()
    {
        var result = _controller.Dismiss("nonexistent") as OkObjectResult;

        Assert.NotNull(result);
        var statusProp = result.Value.GetType().GetProperty("status");
        Assert.NotNull(statusProp);
        Assert.Equal("dismissed", statusProp.GetValue(result.Value)?.ToString());
    }

    [Fact]
    public void ToggleAuto_EnablesAutoMode()
    {
        var result = _controller.ToggleAuto(new AutoModeRequest { Enabled = true }) as OkObjectResult;

        Assert.NotNull(result);
        var autoProp = result.Value.GetType().GetProperty("autoMode");
        Assert.NotNull(autoProp);
        Assert.True((bool)autoProp.GetValue(result.Value)!);
    }

    [Fact]
    public void ToggleAuto_DisablesAutoMode()
    {
        var result = _controller.ToggleAuto(new AutoModeRequest { Enabled = false }) as OkObjectResult;

        Assert.NotNull(result);
        var autoProp = result.Value.GetType().GetProperty("autoMode");
        Assert.NotNull(autoProp);
        Assert.False((bool)autoProp.GetValue(result.Value)!);
    }

    [Fact]
    public void Status_ReturnsStatusInfo()
    {
        var result = _controller.Status() as OkObjectResult;

        Assert.NotNull(result);
        Assert.NotNull(result.Value.GetType().GetProperty("autoMode"));
        Assert.NotNull(result.Value.GetType().GetProperty("lastScan"));
        Assert.NotNull(result.Value.GetType().GetProperty("pendingCount"));
        Assert.NotNull(result.Value.GetType().GetProperty("appliedCount"));
        Assert.NotNull(result.Value.GetType().GetProperty("performance"));
    }

    [Fact]
    public void Performance_ReturnsStatsForAllAgents()
    {
        _perf.RecordSuccess("backend", "api");

        var result = _controller.Performance() as OkObjectResult;

        Assert.NotNull(result);
        var list = result.Value as System.Collections.IList;
        Assert.NotNull(list);
        Assert.Equal(8, list.Count);
    }

    [Fact]
    public async Task Scan_ReturnsReport()
    {
        var result = await _controller.Scan() as OkObjectResult;

        Assert.NotNull(result);
        var report = result.Value as SelfAnalysisReport;
        Assert.NotNull(report);
        Assert.Equal(0, report.TotalFiles);
        Assert.Equal(0, report.TodoCount);
    }
}
