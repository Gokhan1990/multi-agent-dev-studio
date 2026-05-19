using System.Collections.Concurrent;

namespace SaaSFast.Application.Services
{
    public class AgentTaskStats
    {
        public int TotalTasks { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double SuccessRate => TotalTasks > 0 ? (double)SuccessCount / TotalTasks * 100 : 0;
        public string LastTaskType { get; set; } = "";
        public DateTime LastActive { get; set; } = DateTime.MinValue;
        public List<string> RecentTaskTypes { get; set; } = new();
    }

    public class AgentPerformanceTracker
    {
        private readonly ConcurrentDictionary<string, AgentTaskStats> _stats = new();
        private readonly object _lock = new();

        public AgentPerformanceTracker()
        {
            foreach (var agent in AgentRegistry.All)
                _stats[agent.Id] = new AgentTaskStats();
        }

        public void RecordSuccess(string agentId, string taskType)
        {
            _stats.AddOrUpdate(agentId,
                _ => NewStats(taskType, true),
                (_, s) => { s.TotalTasks++; s.SuccessCount++; s.LastTaskType = taskType; s.LastActive = DateTime.UtcNow; s.RecentTaskTypes.Add(taskType); if (s.RecentTaskTypes.Count > 10) s.RecentTaskTypes.RemoveAt(0); return s; });
        }

        public void RecordFailure(string agentId, string taskType)
        {
            _stats.AddOrUpdate(agentId,
                _ => NewStats(taskType, false),
                (_, s) => { s.TotalTasks++; s.FailureCount++; s.LastTaskType = taskType; s.LastActive = DateTime.UtcNow; s.RecentTaskTypes.Add(taskType); if (s.RecentTaskTypes.Count > 10) s.RecentTaskTypes.RemoveAt(0); return s; });
        }

        public AgentTaskStats GetStats(string agentId)
        {
            return _stats.GetOrAdd(agentId, _ => new AgentTaskStats());
        }

        public string GetBestAgentForTask(string taskType, string room)
        {
            var candidates = AgentRegistry.All.Where(a => a.Room == room).ToList();
            if (candidates.Count == 0) return "ceo";

            var scored = candidates.Select(a =>
            {
                var s = GetStats(a.Id);
                var score = s.SuccessRate;
                if (s.RecentTaskTypes.Any(t => t.Contains(taskType) || taskType.Contains(t)))
                    score += 20;
                if (s.LastActive > DateTime.UtcNow.AddMinutes(-5))
                    score += 10;
                return (AgentId: a.Id, Score: score);
            }).OrderByDescending(x => x.Score).ToList();

            return scored.First().AgentId;
        }

        public string GetAllStatsSummary()
        {
            var lines = new List<string>();
            foreach (var kv in _stats.OrderBy(kv => kv.Key))
            {
                var s = kv.Value;
                if (s.TotalTasks > 0)
                    lines.Add($"{kv.Key}: {s.SuccessCount}/{s.TotalTasks} basarili (%{s.SuccessRate:F0})");
            }
            return lines.Count > 0 ? string.Join("; ", lines) : "Henuz gorev yok";
        }

        private static AgentTaskStats NewStats(string taskType, bool success)
        {
            return new AgentTaskStats
            {
                TotalTasks = 1,
                SuccessCount = success ? 1 : 0,
                FailureCount = success ? 0 : 1,
                LastTaskType = taskType,
                LastActive = DateTime.UtcNow,
                RecentTaskTypes = new List<string> { taskType }
            };
        }
    }
}
