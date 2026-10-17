using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NeuralisAPI.Models;

public class PatientLabAttachment
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid PatientUserId { get; set; }

    [ForeignKey(nameof(PatientUserId))]
    public User Patient { get; set; } = null!;

    [Required]
    public Guid UploadedByDoctorProfileId { get; set; }

    [ForeignKey(nameof(UploadedByDoctorProfileId))]
    public DoctorProfile UploadedBy { get; set; } = null!;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Comma-separated classification tags from lab upload.</summary>
    [MaxLength(500)]
    public string Tags { get; set; } = string.Empty;

    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ContentType { get; set; } = string.Empty;

    public byte[] FileBytes { get; set; } = Array.Empty<byte>();

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}
