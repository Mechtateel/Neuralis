using NeuralisAPP_1.Models.Lab;

namespace NeuralisAPP_1.ViewModels.Lab;

public sealed class LabRecentFileViewModel
{
    public Guid AttachmentId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string PatientName { get; init; } = string.Empty;

    public DateTime UploadedAtUtc { get; init; }

    public string TimeDisplay => UploadedAtUtc.ToLocalTime().ToString("g");

    public string Subtitle => $"{PatientName} • {TimeDisplay}";

    public static LabRecentFileViewModel FromDto(LabRecentUploadDto dto) =>
        new()
        {
            AttachmentId = dto.AttachmentId,
            FileName = dto.FileName,
            PatientName = dto.PatientName,
            UploadedAtUtc = dto.UploadedAtUtc
        };
}
