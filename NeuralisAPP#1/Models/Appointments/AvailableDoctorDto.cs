using System.Text.Json.Serialization;
using NeuralisAPP_1.Models.Doctor;

namespace NeuralisAPP_1.Models.Appointments;

public class AvailableDoctorDto
{
    [JsonPropertyName("doctorProfileId")]
    public Guid DoctorProfileId { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("specialization")]
    public string Specialization { get; set; } = string.Empty;

    public string DisplayLabel => $"{FullName} — {DoctorSpecializationUi.Format(Specialization)}";
}
