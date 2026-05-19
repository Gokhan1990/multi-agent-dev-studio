using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SaaSFast.Application.Services;

public class ImprovementSuggestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = ""; // code_quality, performance, agent, system
    public string TargetFile { get; set; } = "";
    public string? SuggestedFix { get; set; }
    public string Status { get; set; } = "pending"; // pending, applied, dismissed, failed
    public int Priority { get; set; } = 5; // 1-10
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AppliedAt { get; set; }
    public string? AppliedBy { get; set; }
    public string? Result { get; set; }
}

public class SelfAnalysisReport
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int TotalFiles { get; set; }
    public int TodoCount { get; set; }
    public int HardcodedValues { get; set; }
    public int SuggestionsGenerated { get; set; }
    public List<string> KeyFindings { get; set; } = new();
    public Dictionary<string, int> AgentPerformance { get; set; } = new();
}

public class SelfImprovementService
{
    private readonly string _sourceRoot;
    private readonly CommandQueueService _queue;
    private readonly AgentMemoryService _memory;
    private readonly AgentPerformanceTracker _performance;
    private readonly OpencodeService _opencode;
    private readonly CodeExecutorService _executor;
    private readonly IConfiguration _config;
    private readonly string _improvementsPath;
    private List<ImprovementSuggestion> _suggestions = new();
    private bool _autoMode;
    private DateTime _lastScan = DateTime.MinValue;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(30);
    private static readonly string[] SourceExtensions = { ".cs", ".js", ".jsx", ".json", ".css", ".ps1" };
    private static readonly string[] HardcodedPatterns = {
        @"https?://localhost:\d+",
        @"password\s*=\s*[""'][^""']+[""']",
        @"api[_-]?key\s*=\s*[""'][^""']+[""']",
        @"secret\s*=\s*[""'][^""']+[""']",
    };

    public SelfImprovementService(IConfiguration config, CommandQueueService queue,
        AgentMemoryService memory, AgentPerformanceTracker performance, OpencodeService opencode,
        CodeExecutorService executor)
    {
        _sourceRoot = config.GetValue<string>("SourceRoot") ?? "/source";
        _queue = queue;
        _memory = memory;
        _performance = performance;
        _opencode = opencode;
        _executor = executor;
        _config = config;
        _improvementsPath = Path.Combine(_sourceRoot, "AgentMemory", "Improvements");
        Directory.CreateDirectory(_improvementsPath);
        _suggestions = LoadSuggestions();
    }

    public bool IsAutoMode => _autoMode;
    public DateTime LastScan => _lastScan;
    public List<ImprovementSuggestion> GetSuggestions(string? status = null) =>
        status != null ? _suggestions.Where(s => s.Status == status).ToList() : _suggestions.ToList();

    public void SetAutoMode(bool enabled)
    {
        _autoMode = enabled;
        if (enabled)
            Task.Run(() => AutoImproveLoop());
    }

    public async Task<SelfAnalysisReport> AnalyzeAsync()
    {
        var report = new SelfAnalysisReport();
        var findings = new List<string>();

        var files = Directory.GetFiles(_sourceRoot, "*.*", SearchOption.AllDirectories)
            .Where(f => SourceExtensions.Contains(Path.GetExtension(f)) &&
                        !f.Contains("node_modules") && !f.Contains("obj") && !f.Contains("bin") &&
                        !f.Contains(".git") && !f.Contains("dist") &&
                        !f.Contains("AgentMemory") && !f.Contains("\\.claude\\"))
            .ToList();

        report.TotalFiles = files.Count;

        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                var relPath = Path.GetRelativePath(_sourceRoot, file);

                if (!relPath.Contains("SelfImprovementService"))
                {
                    var todoMatches = Regex.Matches(content, @"\b(TODO|FIXME|HACK|XXX|BUG|WORKAROUND)\b", RegexOptions.IgnoreCase);
                    report.TodoCount += todoMatches.Count;

                    foreach (Match match in todoMatches)
                    {
                        var lineNum = GetLineNumber(content, match.Index);
                        var line = ExtractLine(content, match.Index).Trim();
                        if (line.Contains("Regex.Matches") || line.Contains(@"\b(TODO|FIXME"))
                            continue;

                        var suggestion = new ImprovementSuggestion
                        {
                            Title = $"Unresolved {match.Value} in {Path.GetFileName(file)}",
                            Description = $"Line {lineNum}: {line}",
                            Category = "code_quality",
                            TargetFile = relPath,
                            Priority = match.Value switch
                            {
                                "BUG" or "FIXME" => 8,
                                "HACK" => 6,
                                _ => 4
                            }
                        };

                        if (!_suggestions.Any(s => s.Title == suggestion.Title && s.TargetFile == relPath && s.Status == "pending"))
                            _suggestions.Add(suggestion);
                    }
                }

                foreach (var pattern in HardcodedPatterns)
                {
                    var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                    foreach (Match match in matches)
                    {
                        var lineNum = GetLineNumber(content, match.Index);
                        var line = ExtractLine(content, match.Index).Trim();
                        if (line.Length > 80) line = line[..80] + "...";

                        var suggestion = new ImprovementSuggestion
                        {
                            Title = $"Possible hardcoded value in {Path.GetFileName(file)}",
                            Description = $"Line {lineNum}: {line}",
                            Category = "security",
                            TargetFile = relPath,
                            Priority = 7
                        };

                        if (!_suggestions.Any(s => s.Title == suggestion.Title && s.TargetFile == relPath && s.Status == "pending"))
                            _suggestions.Add(suggestion);
                    }
                }
            }
            catch { }
        }

        report.AgentPerformance = AnalyzeAgentPerformance();

        report.SuggestionsGenerated = _suggestions.Count(s => s.Status == "pending");
        report.KeyFindings = findings;
        _lastScan = DateTime.UtcNow;

        SaveSuggestions();
        return report;
    }

    public async Task<ImprovementSuggestion?> ApplySuggestionAsync(string suggestionId)
    {
        var suggestion = _suggestions.FirstOrDefault(s => s.Id == suggestionId);
        if (suggestion == null || suggestion.Status != "pending")
            return null;

        suggestion.Status = "applied";
        suggestion.AppliedAt = DateTime.UtcNow;
        suggestion.AppliedBy = "self-improvement";

        var fullPath = Path.Combine(_sourceRoot, suggestion.TargetFile);
        if (File.Exists(fullPath))
        {
            var cmd = _queue.Enqueue(suggestion.Title);
            cmd.ActiveAgentId = DetectBestAgentForFile(suggestion.TargetFile);
            cmd.TargetFile = suggestion.TargetFile;

            _queue.AddLog(cmd.Id, cmd.ActiveAgentId!, $"Self-improvement: {suggestion.Title}", "info");

            var execResult = await _executor.ExecuteAsync(cmd);

            if (execResult.Success)
            {
                suggestion.Result = execResult.Summary;
                _memory.StoreEpisodic("system", suggestion.Title, "self_improvement", execResult.Summary, "completed", execResult.Diff?.DiffText);
            }
            else
            {
                suggestion.Status = "failed";
                suggestion.Result = execResult.Error ?? "Execution failed";
                _memory.StoreEpisodic("system", suggestion.Title, "self_improvement", execResult.Error ?? "failed", "failed");
            }
        }
        else
        {
            suggestion.Result = $"File not found: {suggestion.TargetFile}";
        }

        SaveSuggestions();
        return suggestion;
    }

    public void DismissSuggestion(string suggestionId)
    {
        var s = _suggestions.FirstOrDefault(x => x.Id == suggestionId);
        if (s != null)
        {
            s.Status = "dismissed";
            SaveSuggestions();
        }
    }

    public void RecordOutcome(string taskType, bool success, string? detail = null)
    {
        if (_autoMode && success)
        {
            _memory.StoreEpisodic("system", taskType, "auto_improvement",
                detail ?? "Automatic improvement applied successfully", "completed");
        }
    }

    private async Task AutoImproveLoop()
    {
        while (_autoMode)
        {
            try
            {
                if (DateTime.UtcNow - _lastScan > ScanInterval)
                {
                    var report = await AnalyzeAsync();
                    var pending = _suggestions.Where(s => s.Status == "pending" && s.Priority >= 6).ToList();
                    foreach (var s in pending.Take(3))
                    {
                        await ApplySuggestionAsync(s.Id);
                        await Task.Delay(5000);
                    }
                }
            }
            catch { }
            await Task.Delay(60000);
        }
    }

    private Dictionary<string, int> AnalyzeAgentPerformance()
    {
        var result = new Dictionary<string, int>();
        foreach (var agent in AgentRegistry.All)
        {
            var stats = _performance.GetStats(agent.Id);
            if (stats.TotalTasks > 0)
                result[agent.Id] = (int)stats.SuccessRate;
        }

        var lowPerformers = result.Where(r => r.Value < 50).ToList();
        foreach (var lp in lowPerformers)
        {
            var suggestion = new ImprovementSuggestion
            {
                Title = $"Low performance: {AgentRegistry.GetById(lp.Key).Name} ({lp.Value}%)",
                Description = $"Agent {lp.Key} has {lp.Value}% success rate. Consider reviewing prompt or reassigning tasks.",
                Category = "agent",
                TargetFile = "Backend/Application/Services/AiService.cs",
                Priority = 6
            };
            if (!_suggestions.Any(s => s.Title == suggestion.Title && s.Status == "pending"))
                _suggestions.Add(suggestion);
        }

        return result;
    }

    private string DetectBestAgentForFile(string filePath)
    {
        var lower = filePath.ToLowerInvariant();
        if (lower.Contains("frontend") || lower.Contains(".jsx") || lower.Contains(".js") || lower.Contains(".css"))
            return "deniz";
        if (lower.Contains("backend") || lower.Contains(".cs"))
            return "bora";
        if (lower.Contains("test"))
            return "cem";
        if (lower.Contains("docker") || lower.Contains("deploy") || lower.Contains(".yml"))
            return "sibel";
        return "bora";
    }

    private List<ImprovementSuggestion> LoadSuggestions()
    {
        try
        {
            var path = Path.Combine(_improvementsPath, "suggestions.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<ImprovementSuggestion>>(json) ?? new();
            }
        }
        catch { }
        return new();
    }

    private void SaveSuggestions()
    {
        try
        {
            var path = Path.Combine(_improvementsPath, "suggestions.json");
            File.WriteAllText(path, JsonSerializer.Serialize(_suggestions, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static int GetLineNumber(string content, int index)
    {
        return content[..index].Count(c => c == '\n') + 1;
    }

    private static string ExtractLine(string content, int index)
    {
        var start = content.LastIndexOf('\n', index);
        if (start < 0) start = 0;
        var end = content.IndexOf('\n', index);
        if (end < 0) end = content.Length;
        return content[start..end].Trim();
    }
}
