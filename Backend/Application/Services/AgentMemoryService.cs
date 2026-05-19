using System.Text;
using System.Text.Json;

namespace SaaSFast.Application.Services
{
    public class MemoryEntry
    {
        public string Id { get; set; } = "";
        public string AgentId { get; set; } = "";
        public string Task { get; set; } = "";
        public string Action { get; set; } = "";
        public string Result { get; set; } = "";
        public string Status { get; set; } = "completed";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Diff { get; set; } = "";
    }

    public class ConversationEntry
    {
        public string Id { get; set; } = "";
        public string UserMessage { get; set; } = "";
        public string AgentId { get; set; } = "";
        public string AgentResponse { get; set; } = "";
        public string Room { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool HadCodeChange { get; set; }
        public string CodeResult { get; set; } = "";
    }

    public class AgentMemoryService
    {
        private readonly string _memoryPath;
        private readonly string _conversationPath;
        private readonly string _sourceRoot;
        private readonly List<MemoryEntry> _episodicMemory = new();
        private readonly List<ConversationEntry> _conversationCache = new();

        public AgentMemoryService(IConfiguration config)
        {
            _sourceRoot = config.GetValue<string>("SourceRoot") ?? "/source";
            _memoryPath = Path.Combine(_sourceRoot, "AgentMemory", "EpisodicMemory");
            _conversationPath = Path.Combine(_sourceRoot, "AgentMemory", "Conversations");
            Directory.CreateDirectory(_memoryPath);
            Directory.CreateDirectory(_conversationPath);
            _conversationCache = LoadConversations();
        }

        public void StoreConversation(string userMessage, string agentId, string agentResponse, string room, bool hadCodeChange, string codeResult = "")
        {
            var entry = new ConversationEntry
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                UserMessage = userMessage,
                AgentId = agentId,
                AgentResponse = agentResponse,
                Room = room,
                Timestamp = DateTime.UtcNow,
                HadCodeChange = hadCodeChange,
                CodeResult = codeResult
            };

            _conversationCache.Add(entry);
            if (_conversationCache.Count > 200)
                _conversationCache.RemoveAt(0);

            try
            {
                var filePath = Path.Combine(_conversationPath, "conversations.json");
                var existing = new List<ConversationEntry>();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    existing = JsonSerializer.Deserialize<List<ConversationEntry>>(json) ?? new();
                }
                existing.Add(entry);
                if (existing.Count > 500)
                    existing = existing.Skip(existing.Count - 500).ToList();
                File.WriteAllText(filePath, JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public List<ConversationEntry> GetRecentConversations(int count = 10)
        {
            return _conversationCache
                .OrderByDescending(c => c.Timestamp)
                .Take(count)
                .ToList();
        }

        public string GetConversationContext(string agentId, string room, int count = 5)
        {
            var relevant = _conversationCache
                .Where(c => c.Room == room)
                .OrderByDescending(c => c.Timestamp)
                .Take(count)
                .ToList();

            if (relevant.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("\nSon konusmalar:");
            foreach (var c in relevant)
            {
                var agentName = AgentRegistry.GetById(c.AgentId).Name;
                var codeIcon = c.HadCodeChange ? "🔧" : "";
                sb.AppendLine($"- Kullanici: {c.UserMessage}");
                sb.AppendLine($"  {agentName}: {c.AgentResponse} {codeIcon}");
            }
            return sb.ToString();
        }

        public string GetAgentHistoryContext(string agentId, int count = 3)
        {
            var entries = _conversationCache
                .Where(c => c.AgentId == agentId)
                .OrderByDescending(c => c.Timestamp)
                .Take(count)
                .ToList();

            if (entries.Count == 0)
            {
                var filePath = Path.Combine(_memoryPath, $"{agentId}.json");
                if (File.Exists(filePath))
                {
                    try
                    {
                        var json = File.ReadAllText(filePath);
                        var memories = JsonSerializer.Deserialize<List<MemoryEntry>>(json) ?? new();
                        var recent = memories.OrderByDescending(m => m.Timestamp).Take(count).ToList();
                        if (recent.Count > 0)
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine("\nGecmis gorevlerin:");
                            foreach (var m in recent)
                            {
                                var icon = m.Status == "completed" || m.Status == "success" ? "✅" : "❌";
                                sb.AppendLine($"- {icon} {m.Task}: {m.Result}");
                            }
                            return sb.ToString();
                        }
                    }
                    catch { }
                }
                return "";
            }

            var s = new StringBuilder();
            s.AppendLine("\nOnceki konusmalarin:");
            foreach (var e in entries)
            {
                var codeIcon = e.HadCodeChange ? " 🔧" : "";
                s.AppendLine($"- Kullanici: \"{e.UserMessage}\"");
                s.AppendLine($"  Sen: \"{e.AgentResponse}\"{codeIcon}");
            }
            return s.ToString();
        }

        public void StoreEpisodic(string agentId, string task, string action, string result, string status, string diff = "")
        {
            var entry = new MemoryEntry
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                AgentId = agentId,
                Task = task.Length > 100 ? task[..100] + "..." : task,
                Action = action,
                Result = result.Length > 200 ? result[..200] + "..." : result,
                Status = status,
                Timestamp = DateTime.UtcNow,
                Diff = diff.Length > 500 ? diff[..500] + "..." : diff
            };

            _episodicMemory.Add(entry);
            if (_episodicMemory.Count > 50)
                _episodicMemory.RemoveAt(0);

            try
            {
                var filePath = Path.Combine(_memoryPath, $"{agentId}.json");
                var existing = new List<MemoryEntry>();
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    existing = JsonSerializer.Deserialize<List<MemoryEntry>>(json) ?? new();
                }
                existing.Add(entry);
                if (existing.Count > 100)
                    existing = existing.Skip(existing.Count - 100).ToList();
                File.WriteAllText(filePath, JsonSerializer.Serialize(existing, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public string GetRecentContext(string agentId, int count = 3)
        {
            var relevant = _episodicMemory
                .Where(m => m.AgentId == agentId || m.AgentId == "all")
                .OrderByDescending(m => m.Timestamp)
                .Take(count)
                .ToList();

            if (relevant.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("\nGecmis deneyimlerin:");
            foreach (var m in relevant)
            {
                var statusEmoji = m.Status == "success" || m.Status == "completed" ? "✅" : "❌";
                sb.AppendLine($"- {statusEmoji} {m.Task} -> {m.Result}");
            }
            return sb.ToString();
        }

        public string GetPeerReviewContext(string agentId, int count = 2)
        {
            var peers = _episodicMemory
                .Where(m => m.AgentId != agentId && m.Status == "completed")
                .OrderByDescending(m => m.Timestamp)
                .Take(count)
                .ToList();

            if (peers.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("\nEkip arkadaslarinin son basarilari:");
            foreach (var p in peers)
                sb.AppendLine($"- {p.AgentId}: {p.Task} -> {p.Result}");
            return sb.ToString();
        }

        private List<ConversationEntry> LoadConversations()
        {
            try
            {
                var filePath = Path.Combine(_conversationPath, "conversations.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    return JsonSerializer.Deserialize<List<ConversationEntry>>(json) ?? new();
                }
            }
            catch { }
            return new();
        }
    }
}
