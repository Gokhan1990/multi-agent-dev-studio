using Xunit;
using Microsoft.Extensions.Configuration;
using SaaSFast.Application.Services;

namespace Backend.Tests;

public class CommandQueueServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CommandQueueService _queue;

    public CommandQueueServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SaaSFast_Tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CommandQueuePath"] = _tempDir
            })
            .Build();

        _queue = new CommandQueueService(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Enqueue_AddsCommandWithPendingStatus()
    {
        var cmd = _queue.Enqueue("backend api ekle");
        Assert.Equal("pending", cmd.Status);
        Assert.False(string.IsNullOrWhiteSpace(cmd.Id));
    }

    [Fact]
    public void Enqueue_ParsesTextAndSetsTargetFile()
    {
        var cmd = _queue.Enqueue("backend api ekle");
        Assert.Contains("Backend", cmd.TargetFile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Enqueue_DetectsCreateChangeType()
    {
        var cmd = _queue.Enqueue("yeni bir dosya ekle");
        Assert.Equal("create", cmd.ChangeType);
    }

    [Fact]
    public void Enqueue_DetectsDeleteChangeType()
    {
        var cmd = _queue.Enqueue("dosyayı sil");
        Assert.Equal("delete", cmd.ChangeType);
    }

    [Fact]
    public void Enqueue_DetectsEditChangeTypeByDefault()
    {
        var cmd = _queue.Enqueue("şu kısmı düzelt");
        Assert.Equal("edit", cmd.ChangeType);
    }

    [Fact]
    public void Enqueue_DetectsVoiceConfigChangeType()
    {
        var cmd = _queue.Enqueue("ses pitch değerini değiştir");
        Assert.Equal("voice_config", cmd.ChangeType);
    }

    [Fact]
    public void Enqueue_SetsSummary()
    {
        var cmd = _queue.Enqueue("backend api ekle");
        Assert.False(string.IsNullOrWhiteSpace(cmd.Summary));
    }

    [Fact]
    public void Enqueue_GeneratedIdIsEightChars()
    {
        var cmd = _queue.Enqueue("test");
        Assert.Equal(8, cmd.Id.Length);
    }

    [Fact]
    public void GetPending_ReturnsPendingCommands()
    {
        _queue.Enqueue("komut 1");
        _queue.Enqueue("komut 2");
        var pending = _queue.GetPending();
        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public void GetPending_ExcludesProcessedCommands()
    {
        var cmd = _queue.Enqueue("test");
        _queue.MarkProcessed(cmd.Id);
        var pending = _queue.GetPending();
        Assert.DoesNotContain(pending, c => c.Id == cmd.Id);
    }

    [Fact]
    public void MarkProcessed_SetsCompletedStatus()
    {
        var cmd = _queue.Enqueue("test");
        _queue.MarkProcessed(cmd.Id);
        var all = _queue.GetRecent(10);
        var processed = all.First(c => c.Id == cmd.Id);
        Assert.Equal("completed", processed.Status);
        Assert.NotNull(processed.ProcessedAt);
    }

    [Fact]
    public void MarkProcessed_SetsFailedStatusWithError()
    {
        var cmd = _queue.Enqueue("test");
        _queue.MarkProcessed(cmd.Id, "something went wrong");
        var all = _queue.GetRecent(10);
        var processed = all.First(c => c.Id == cmd.Id);
        Assert.Equal("failed", processed.Status);
        Assert.Equal("something went wrong", processed.Error);
    }

    [Fact]
    public void AddLog_AddsLogEntryAndSetsProcessing()
    {
        var cmd = _queue.Enqueue("test");
        _queue.AddLog(cmd.Id, "bora", "starting work");

        var active = _queue.GetActive();
        var updated = active.First(c => c.Id == cmd.Id);
        Assert.Equal("processing", updated.Status);
        Assert.Equal("bora", updated.ActiveAgentId);
    }

    [Fact]
    public void GetFeed_ReturnsLogsOrderedByTime()
    {
        var cmd = _queue.Enqueue("test");
        _queue.AddLog(cmd.Id, "bora", "first");
        Thread.Sleep(10);
        _queue.AddLog(cmd.Id, "bora", "second");

        var feed = _queue.GetFeed(10);
        Assert.Equal(2, feed.Count);
        Assert.Equal("second", feed[0].Text);
    }

    [Fact]
    public void AddStep_AppendsStep()
    {
        var cmd = _queue.Enqueue("test");
        _queue.AddStep(cmd.Id, "bora", "reading file");

        var active = _queue.GetActive();
        var updated = active.First(c => c.Id == cmd.Id);
        Assert.Single(updated.Steps);
        Assert.Equal("reading file", updated.Steps[0].Action);
    }

    [Fact]
    public void GetActive_ReturnsPendingAndProcessingCommands()
    {
        var cmd1 = _queue.Enqueue("first");
        var cmd2 = _queue.Enqueue("second");
        _queue.AddLog(cmd1.Id, "bora", "working");

        var active = _queue.GetActive();
        Assert.Contains(active, c => c.Id == cmd1.Id);
        Assert.Contains(active, c => c.Id == cmd2.Id);
    }

    [Fact]
    public void GetActive_ExcludesCompletedCommands()
    {
        var cmd = _queue.Enqueue("test");
        _queue.MarkProcessed(cmd.Id);
        var active = _queue.GetActive();
        Assert.DoesNotContain(active, c => c.Id == cmd.Id);
    }

    [Fact]
    public void GetRecent_ReturnsLatestCommands()
    {
        for (int i = 0; i < 5; i++)
            _queue.Enqueue($"command {i}");

        var recent = _queue.GetRecent(3);
        Assert.Equal(3, recent.Count);
    }
}
