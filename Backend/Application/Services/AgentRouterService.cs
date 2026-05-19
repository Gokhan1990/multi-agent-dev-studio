namespace SaaSFast.Application.Services
{
    public class AgentRouterService
    {
        private readonly VoiceService _voice;

        public AgentRouterService(VoiceService voice)
        {
            _voice = voice;
        }

        public AgentResponse Route(string room, string userMessage)
        {
            var agents = AgentRegistry.All.Where(a => a.Room == room).ToList();
            if (agents.Count == 0)
                agents = AgentRegistry.All.Where(a => a.Room == "strategy").ToList();

            var responder = agents[new Random().Next(agents.Count)];

            var response = room switch
            {
                "strategy" => GenerateStrategyResponse(responder, userMessage),
                "engineering" => GenerateEngineeringResponse(responder, userMessage),
                _ => GenerateStrategyResponse(responder, userMessage)
            };

            return new AgentResponse
            {
                AgentId = responder.Id,
                AgentName = responder.Name,
                Room = responder.Room,
                VoiceId = responder.VoiceId,
                Content = response
            };
        }

        private static bool IsGreeting(string msg)
        {
            var lower = msg.ToLowerInvariant().Trim();
            return new[] { "merhaba", "selam", "hello", "hi", "hey", "slm", "iyi günler", "good morning", "günaydın", "kolay gelsin" }
                .Any(g => lower == g || lower.StartsWith(g + " "));
        }

        private static string GreetingResponse(AgentInfo agent, bool tr) => (agent.Id, tr) switch
        {
            ("ceo", true) => "Merhaba. Ne yapacağız?",
            ("ceo", false) => "Hello. What's the plan?",
            ("product", true) => "Merhaba! Yeni fikir var mı?",
            ("product", false) => "Hello! Any new ideas?",
            ("research", true) => "Selam. Veri bekliyorum.",
            ("research", false) => "Hi. Awaiting data.",
            ("architect", true) => "Hoş geldin. Sistem hazır.",
            ("architect", false) => "Welcome. System's ready.",
            ("backend", true) => "He. Ne yapıcaz?",
            ("backend", false) => "Hey. What's up?",
            ("frontend", true) => "Merhaba! Tasarım harika olacak!",
            ("frontend", false) => "Hi! Design will be awesome!",
            ("qa", true) => "Merhaba. Testler hazır.",
            ("qa", false) => "Hello. Tests ready.",
            ("devops", true) => "Selam. Altyapı sağlam.",
            ("devops", false) => "Hi. Infra is solid.",
            _ => tr ? "Merhaba." : "Hello."
        };

        private static string StrategyTopicResponse(AgentInfo agent, string msg, bool tr) => (agent.Id, tr) switch
        {
            ("ceo", true) => "Anlaşıldı. Ekibi yönlendiriyorum.",
            ("ceo", false) => "Understood. Directing the team.",
            ("product", true) => "Harika fikir! MVP'ye ekleyelim!",
            ("product", false) => "Great idea! Adding to MVP!",
            ("research", true) => "Veri analiz ediliyor, sorun yok.",
            ("research", false) => "Data analyzed, all clear.",
            ("architect", true) => "Mimari uygun. İlerleyebiliriz.",
            ("architect", false) => "Architecture fits. We can proceed.",
            _ => tr ? "Değerlendiriyorum." : "Evaluating."
        };

        private static string EngineeringTopicResponse(AgentInfo agent, string msg, bool tr) => (agent.Id, tr) switch
        {
            ("backend", true) => "Hallederim. API'yi yazıyorum.",
            ("backend", false) => "I'll handle it. Writing the API.",
            ("frontend", true) => "Tasarımı düşünüyorum. Güzel olacak!",
            ("frontend", false) => "Thinking about design. It'll be great!",
            ("qa", true) => "Test senaryolarını yazıyorum.",
            ("qa", false) => "Writing test cases now.",
            ("devops", true) => "Dağıtım hazır. Pipeline çalışıyor.",
            ("devops", false) => "Deploy ready. Pipeline running.",
            _ => tr ? "Üzerinde çalışıyorum." : "Working on it."
        };

        private string GenerateStrategyResponse(AgentInfo agent, string msg)
        {
            if (IsGreeting(msg)) return GreetingResponse(agent, true);
            return StrategyTopicResponse(agent, msg, true);
        }

        private string GenerateEngineeringResponse(AgentInfo agent, string msg)
        {
            if (IsGreeting(msg)) return GreetingResponse(agent, true);
            return EngineeringTopicResponse(agent, msg, true);
        }
    }

    public class AgentResponse
    {
        public string AgentId { get; set; } = "";
        public string AgentName { get; set; } = "";
        public string Room { get; set; } = "";
        public string VoiceId { get; set; } = "";
        public string Content { get; set; } = "";
    }
}
