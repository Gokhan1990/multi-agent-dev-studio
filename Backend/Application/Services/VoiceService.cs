namespace SaaSFast.Application.Services
{
    public class VoiceService
    {
        // Test temiz log
        public string GetVoiceId(string agentId)
        {
            var agent = AgentRegistry.GetById(agentId);
            return agent.VoiceId;
        }

        public Dictionary<string, string> GetAllVoices()
        {
            return AgentRegistry.All.ToDictionary(a => a.Id, a => a.VoiceId);
        }

        // Test temiz log.
        // opencode AI ile canli transcript testi
    }
}
// VoiceService v2.0 - updated by opencode
// Watcher v3 test
