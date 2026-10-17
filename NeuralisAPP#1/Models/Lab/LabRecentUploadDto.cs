using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Lab;

public class LabRecentUploadDto
{
    [JsonPropertyName("attachmentId")]
    public Guid AttachmentId { get; set; }

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("patientUserId")]
    public Guid PatientUserId { get; set; }

    [JsonPropertyName("patientName")]
    public string PatientName { get; set; } = string.Empty;

    [JsonPropertyName("uploadedAtUtc")]
    public DateTime UploadedAtUtc { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long? SizeBytes { get; set; }

    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    [JsonPropertyName("uploadedByName")]
    public string? UploadedByName { get; set; }
}
