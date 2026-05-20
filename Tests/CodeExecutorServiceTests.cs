using Xunit;
using SaaSFast.Application.Services;

namespace Tests;

public class CodeExecutorServiceTests
{
    [Fact]
    public void ComputeDiff_IdenticalContent_ReturnsEmptyDiff()
    {
        var content = "line1\nline2\nline3";
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("ComputeDiff", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, new object[] { "test.cs", content, content }) as FileDiff;

        Assert.NotNull(result);
        Assert.Equal(0, result.LinesAdded);
        Assert.Equal(0, result.LinesRemoved);
    }

    [Fact]
    public void ExtractCode_RemovesMarkdownFences()
    {
        var raw = "```\ncode here\n```";
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("ExtractCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, new object[] { raw, ".cs" }) as string;
        Assert.Equal("code here", result);
    }

    [Fact]
    public void ExtractCode_RemovesLanguageSpecificFences()
    {
        var raw = "```csharp\nvar x = 1;\n```";
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("ExtractCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, new object[] { raw, ".cs" }) as string;
        Assert.Equal("var x = 1;", result);
    }

    [Fact]
    public void ExtractCode_ReturnsRaw_WhenNoFences()
    {
        var raw = "just some text";
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("ExtractCode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, new object[] { raw, ".cs" }) as string;
        Assert.Equal("just some text", result);
    }

    [Fact]
    public void Slugify_ReplacesTurkishChars()
    {
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("Slugify", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, new object[] { "şeker ölçer" }) as string;
        Assert.Equal("seker-olcer", result);
    }

    [Fact]
    public void Slugify_ReplacesSpacesWithHyphens()
    {
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("Slugify", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, new object[] { "hello world test" }) as string;
        Assert.Equal("hello-world-test", result);
    }

    [Fact]
    public void Slugify_RemovesSpecialChars()
    {
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("Slugify", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result = method.Invoke(null, new object[] { "hello! world? test." }) as string;
        Assert.Equal("hello-world-test", result);
    }

    [Fact]
    public void IsNewProjectRequest_DetectsProjectKeywords()
    {
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("IsNewProjectRequest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        var result1 = (bool)method.Invoke(null, new object[] { "yeni bir site yap" })!;
        var result2 = (bool)method.Invoke(null, new object[] { "normal bir komut" })!;

        Assert.True(result1);
        Assert.False(result2);
    }

    [Fact]
    public void InferTargetFile_DetectsBackendKeyword()
    {
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("InferTargetFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var instance = new PrivateType();
        var result = method.Invoke(instance.GetInstance(), new object[] { "backend api ekle" }) as string;
        Assert.Contains("Backend", result);
    }

    [Fact]
    public void InferTargetFile_DetectsFrontendKeyword()
    {
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("InferTargetFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var instance = new PrivateType();
        var result = method.Invoke(instance.GetInstance(), new object[] { "frontend ui düzenle" }) as string;
        Assert.Contains("frontend", result);
    }

    [Fact]
    public void InferTargetFile_DetectsNewProject()
    {
        var type = typeof(CodeExecutorService);
        var method = type.GetMethod("InferTargetFile", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        var instance = new PrivateType();
        var result = method.Invoke(instance.GetInstance(), new object[] { "yeni bir site yap" }) as string;
        Assert.Contains("generated_projects", result);
    }

    private class PrivateType
    {
        private readonly CodeExecutorService _instance;

        public PrivateType()
        {
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SourceRoot"] = "/tmp"
                })
                .Build();
            var queue = new CommandQueueService(config);
            var http = new HttpClient();
            var memory = new AgentMemoryService(config);
            var perf = new AgentPerformanceTracker();
            var review = new CodeReviewService();
            var opencode = new OpencodeService(config);
            _instance = new CodeExecutorService(config, queue, http, memory, perf, review, opencode);
        }

        public object GetInstance() => _instance;
    }
}
