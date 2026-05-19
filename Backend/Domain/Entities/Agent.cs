using System.ComponentModel.DataAnnotations;

namespace SaaSFast.Domain.Entities
{
    public class Agent
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Role { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Room { get; set; } = string.Empty;

        [MaxLength(50)]
        public string VoiceId { get; set; } = string.Empty;

        public bool Active { get; set; } = true;
    }
}
