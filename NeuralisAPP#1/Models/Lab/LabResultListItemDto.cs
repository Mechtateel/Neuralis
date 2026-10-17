using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Lab;

public class LabResultListItemDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("patientUserId")]
    public Guid PatientUserId { get; set; }

    [JsonPropertyName("patientFullName")]
    public string PatientFullName { get; set; } = string.Empty;

    [JsonPropertyName("patientEmail")]
    public string PatientEmail { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("uploadedAtUtc")]
    public DateTime UploadedAtUtc { get; set; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;
}
