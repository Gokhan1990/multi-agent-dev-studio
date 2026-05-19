namespace SaaSFast.Application.Services
{
    public static class AgentRegistry
    {
        public static List<AgentInfo> All => new()
        {
            new("ceo", "CEO ORCHESTRATOR", "strategy", "Google UK English Male"),
            new("product", "Product Strategist", "strategy", "Google UK English Female"),
            new("research", "Research Agent", "strategy", "Google UK English Male"),
            new("architect", "System Architect", "strategy", "Google UK English Female"),
            new("backend", "Backend Developer", "engineering", "Google UK English Male"),
            new("frontend", "Frontend Developer", "engineering", "Google UK English Female"),
            new("qa", "QA Agent", "engineering", "Google UK English Male"),
            new("devops", "DevOps Agent", "engineering", "Google UK English Female"),
        };

        public static AgentInfo GetById(string id) =>
            All.FirstOrDefault(a => a.Id == id) ?? All[0];
    }

    public record AgentInfo(string Id, string Name, string Room, string VoiceId);
}
