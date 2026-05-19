using System.ComponentModel.DataAnnotations;

namespace SaaSFast.Domain.Entities
{
    public class Room
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;
    }
}
