namespace SaaSFast.Application.Services
{
    public class OrchestratorService
    {
        private readonly AgentRouterService _agentRouter;
        private readonly VoiceService _voice;

        public OrchestratorService(AgentRouterService agentRouter, VoiceService voice)
        {
            _agentRouter = agentRouter;
            _voice = voice;
        }

        public OrchestratedResponse ProcessMessage(string message, string room)
        {
            var agentResponse = _agentRouter.Route(room, message);

            return new OrchestratedResponse
            {
                UserMessage = message,
                AgentId = agentResponse.AgentId,
                AgentName = agentResponse.AgentName,
                Room = agentResponse.Room,
                VoiceId = agentResponse.VoiceId,
                Content = agentResponse.Content,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public class OrchestratedResponse
    {
        public string UserMessage { get; set; } = "";
        public string AgentId { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string Room { get; set; } = "";
        public string VoiceId { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; }
    }
}
