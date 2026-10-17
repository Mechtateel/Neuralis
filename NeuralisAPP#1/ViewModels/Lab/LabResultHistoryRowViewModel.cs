using NeuralisAPP_1.Models.Lab;

namespace NeuralisAPP_1.ViewModels.Lab;

public sealed class LabResultHistoryRowViewModel
{
    public Guid AttachmentId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public DateTime UploadedAtUtc { get; init; }

    public long SizeBytes { get; init; }

    public string UploadedByName { get; init; } = string.Empty;

    public string? Tags { get; init; }

    public string? Description { get; init; }

    public string TimeDisplay => UploadedAtUtc.ToLocalTime().ToString("g");

    public string TagsDisplay => string.IsNullOrWhiteSpace(Tags) ? "—" : Tags!;

    public string DescriptionDisplay => string.IsNullOrWhiteSpace(Description) ? "—" : Description!;

    public string SizeDisplay
    {
        get
        {
            if (SizeBytes < 1024)
                return $"{SizeBytes} B";
            if (SizeBytes < 1024 * 1024)
                return $"{SizeBytes / 1024.0:0.#} KB";
            return $"{SizeBytes / (1024.0 * 1024):0.#} MB";
        }
    }

    public string SubtitleLine => $"{TimeDisplay} • {SizeDisplay} • {UploadedByName}";

    public static LabResultHistoryRowViewModel FromDto(LabUploadHistoryDto dto) =>
        new()
        {
            AttachmentId = dto.AttachmentId,
            FileName = dto.FileName,
            UploadedAtUtc = dto.UploadedAtUtc,
            SizeBytes = dto.SizeBytes,
            UploadedByName = dto.UploadedByName,
            Tags = dto.Tags,
            Description = dto.Description
        };
}
