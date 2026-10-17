using NeuralisAPI.Models;

namespace NeuralisAPI.Contracts.Admin
{
    public class DoctorApplicationResponse
    {
        public Guid DoctorId { get; set; }
        public Guid UserId { get; set; }

        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;

        public string Specialization { get; set; } = null!;
        public string LicenseNumber { get; set; } = null!;

        public DoctorApplicationStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        public string? RejectionReason { get; set; }

        // AI (пока заглушка)
        public int? AiMatchScore { get; set; }
        public string? AiExplanation { get; set; }
    }
}
