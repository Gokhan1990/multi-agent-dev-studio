using System.Text.Json;

namespace SaaSFast.Application.Services
{
    public class CommandStep
    {
        public string AgentId { get; set; } = "";
        public string Action { get; set; } = "";
        public string Status { get; set; } = "processing";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class LogEntry
    {
        public string Text { get; set; } = "";
        public string Level { get; set; } = "info";
        public string AgentId { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class CodeCommand
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Text { get; set; } = "";
        public string? TargetFile { get; set; }
        public string? ChangeType { get; set; }
        public string? Summary { get; set; }
        public string Status { get; set; } = "pending";
        public string? ActiveAgentId { get; set; }
        public List<CommandStep> Steps { get; set; } = new();
        public List<LogEntry> Logs { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public string? Error { get; set; }
    }

    public class CommandQueueService
    {
        private readonly string _filePath;
        private readonly object _lock = new();
        private static readonly TimeSpan ProcessingTimeout = TimeSpan.FromMinutes(30);

        public CommandQueueService(IConfiguration config)
        {
            var basePath = config.GetValue<string>("CommandQueuePath") ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Commands");
            Directory.CreateDirectory(basePath);
            _filePath = Path.Combine(basePath, "queue.json");
            if (!File.Exists(_filePath))
                File.WriteAllText(_filePath, "[]");
        }

        public CodeCommand Enqueue(string text)
        {
            var parsed = ParseText(text);
            var commands = ReadAll();
            commands.Add(parsed);
            WriteAll(commands);
            return parsed;
        }

        public List<CodeCommand> GetPending()
        {
            TimeoutStaleCommands();
            return ReadAll().Where(c => c.Status == "pending").ToList();
        }

        public List<CodeCommand> GetRecent(int count = 10)
        {
            TimeoutStaleCommands();
            return ReadAll()
                .OrderByDescending(c => c.CreatedAt)
                .Take(count)
                .ToList();
        }

        public List<CodeCommand> GetActive()
        {
            TimeoutStaleCommands();
            return ReadAll().Where(c => c.Status == "pending" || c.Status == "processing").ToList();
        }

        public void MarkProcessed(string id, string? error = null)
        {
            var commands = ReadAll();
            var cmd = commands.FirstOrDefault(c => c.Id == id);
            if (cmd != null)
            {
                cmd.Status = error != null ? "failed" : "completed";
                cmd.ProcessedAt = DateTime.UtcNow;
                cmd.Error = error;
                cmd.ActiveAgentId = null;
                WriteAll(commands);
            }
        }

        public void AddLog(string id, string agentId, string text, string level = "info")
        {
            var commands = ReadAll();
            var cmd = commands.FirstOrDefault(c => c.Id == id);
            if (cmd != null)
            {
                cmd.Status = "processing";
                cmd.ActiveAgentId = agentId;
                cmd.Logs.Add(new LogEntry
                {
                    Text = text,
                    Level = level,
                    AgentId = agentId,
                    Timestamp = DateTime.UtcNow
                });
                WriteAll(commands);
            }
        }

        public List<LogEntry> GetFeed(int count = 100)
        {
            TimeoutStaleCommands();
            return ReadAll()
                .SelectMany(c => c.Logs)
                .OrderByDescending(l => l.Timestamp)
                .Take(count)
                .ToList();
        }

        public void AddStep(string id, string agentId, string action)
        {
            var commands = ReadAll();
            var cmd = commands.FirstOrDefault(c => c.Id == id);
            if (cmd != null)
            {
                cmd.Status = "processing";
                cmd.ActiveAgentId = agentId;
                cmd.Steps.Add(new CommandStep
                {
                    AgentId = agentId,
                    Action = action,
                    Status = "completed",
                    Timestamp = DateTime.UtcNow
                });
                WriteAll(commands);
            }
        }

        public int TimeoutStaleCommands()
        {
            var commands = ReadAll();
            var stale = commands.Where(c =>
                c.Status == "processing" &&
                c.CreatedAt < DateTime.UtcNow - ProcessingTimeout).ToList();

            if (stale.Count == 0)
                return 0;

            foreach (var cmd in stale)
            {
                cmd.Status = "failed";
                cmd.ProcessedAt = DateTime.UtcNow;
                cmd.Error = "Command timed out after 30 minutes in processing state.";
                cmd.ActiveAgentId = null;
            }

            WriteAll(commands);
            return stale.Count;
        }

        private List<CodeCommand> ReadAll()
        {
            lock (_lock)
            {
                try
                {
                    var json = File.ReadAllText(_filePath);
                    return JsonSerializer.Deserialize<List<CodeCommand>>(json) ?? new();
                }
                catch { return new(); }
            }
        }

        private void WriteAll(List<CodeCommand> commands)
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(commands, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
        }

        private CodeCommand ParseText(string text)
        {
            var cmd = new CodeCommand { Text = text };
            var lower = text.ToLowerInvariant();

            if (lower.Contains("ses") || lower.Contains("voice") || lower.Contains("pitch") || lower.Contains("rate"))
                cmd.ChangeType = "voice_config";
            else if (lower.Contains("ekle") || lower.Contains("add") || lower.Contains("create") || lower.Contains("yeni") || lower.Contains("yap"))
                cmd.ChangeType = "create";
            else if (lower.Contains("sil") || lower.Contains("delete") || lower.Contains("remove") || lower.Contains("çıkar"))
                cmd.ChangeType = "delete";
            else
                cmd.ChangeType = "edit";

            cmd.Summary = text.Length > 50 ? text[..50] + "..." : text;
            return cmd;
        }
    }
}