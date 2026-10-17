using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Appointments;

public class DoctorAppointmentDto
{
    [JsonPropertyName("appointmentId")]
    public Guid AppointmentId { get; set; }

    [JsonPropertyName("id")]
    public string? AppointmentIdAlias
    {
        set
        {
            if (Guid.TryParse(value, out var parsed))
                AppointmentId = parsed;
        }
    }

    [JsonPropertyName("scheduledAtUtc")]
    public DateTime ScheduledAtUtc { get; set; }

    [JsonPropertyName("dateTime")]
    public DateTime DateTimeAlias
    {
        set => ScheduledAtUtc = value;
    }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("patientUserId")]
    public Guid PatientUserId { get; set; }

    [JsonPropertyName("patientEmail")]
    public string PatientEmail { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string EmailAlias
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                PatientEmail = value;
        }
    }

    [JsonPropertyName("patientFullName")]
    public string PatientFullName { get; set; } = string.Empty;

    [JsonPropertyName("patientName")]
    public string PatientNameAlias
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
                PatientFullName = value;
        }
    }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonIgnore]
    public string PatientDisplay =>
        string.IsNullOrWhiteSpace(PatientFullName) ? PatientEmail : PatientFullName;

    [JsonIgnore]
    public string ScheduledLocalDisplay => $"Local: {ScheduledAtUtc.ToLocalTime():g}";
}
