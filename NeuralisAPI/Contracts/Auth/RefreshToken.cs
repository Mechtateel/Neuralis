using NeuralisAPI.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeuralisAPI.Contracts.Auth
{
    public class RefreshToken
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Token { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }

        public bool IsRevoked { get; set; } = false;

        // 🔗 связь с пользователем
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;
    }
}
