using NeuralisAPI.Contracts.Auth;
using System.ComponentModel.DataAnnotations;

namespace NeuralisAPI.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        [MaxLength(100)]
        public string FullName { get; set; } = null!;

        public string Email { get; set; } = null!;

        public string PasswordHash { get; set; } = null!;

        public UserRole Role { get; set; } = UserRole.User;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        public bool IsBlocked { get; set; } = false;

        public DoctorProfile? DoctorProfile { get; set; }

        public ICollection<Appointment> AppointmentsAsPatient { get; set; } = new List<Appointment>();
    }
}
