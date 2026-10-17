using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeuralisAPI.Models
{
    public class Appointment
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid PatientUserId { get; set; }

        [ForeignKey(nameof(PatientUserId))]
        public User Patient { get; set; } = null!;

        [Required]
        public Guid DoctorProfileId { get; set; }

        [ForeignKey(nameof(DoctorProfileId))]
        public DoctorProfile DoctorProfile { get; set; } = null!;

        public DateTime ScheduledAtUtc { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
