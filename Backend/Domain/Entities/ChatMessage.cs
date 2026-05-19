using System.ComponentModel.DataAnnotations;

namespace SaaSFast.Domain.Entities
{
    public class ChatMessage
    {
        public int Id { get; set; }

        public string Content { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Sender { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Room { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string VoiceId { get; set; } = string.Empty;

        public bool IsUser { get; set; }
    }
}
