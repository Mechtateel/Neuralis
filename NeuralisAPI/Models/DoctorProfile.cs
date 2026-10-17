using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeuralisAPI.Models
{
    public class DoctorProfile
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid UserId { get; set; }

        [ForeignKey(nameof(UserId))]
        public User User { get; set; } = null!;

        [MaxLength(100)]
        public string Specialization { get; set; } = null!;

        [MaxLength(50)]
        public string LicenseNumber { get; set; } = null!;

        public bool IsVerified { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DoctorApplicationStatus Status { get; set; }
      

        public DateTime? ReviewedAt { get; set; }
        public Guid? ReviewedByAdminId { get; set; }
        public string? RejectionReason { get; set; }

        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
