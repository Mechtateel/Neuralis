using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Lab;

public class LabAssignedPatientDto
{
    [JsonPropertyName("patientUserId")]
    public Guid PatientUserId { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("hasUploadedResults")]
    public bool HasUploadedResults { get; set; }

    public string DisplayLabel => string.IsNullOrWhiteSpace(FullName)
        ? Email
        : $"{FullName} ({Email})";
}
