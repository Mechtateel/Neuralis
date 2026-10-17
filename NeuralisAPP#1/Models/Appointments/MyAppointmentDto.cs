using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Appointments;

public class MyAppointmentDto
{
    [JsonPropertyName("appointmentId")]
    public Guid AppointmentId { get; set; }

    [JsonPropertyName("scheduledAtUtc")]
    public DateTime ScheduledAtUtc { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("doctorProfileId")]
    public Guid DoctorProfileId { get; set; }

    [JsonPropertyName("doctorSpecialization")]
    public string DoctorSpecialization { get; set; } = string.Empty;

    [JsonPropertyName("doctorEmail")]
    public string DoctorEmail { get; set; } = string.Empty;

    [JsonPropertyName("doctorFullName")]
    public string DoctorFullName { get; set; } = string.Empty;

    [JsonIgnore]
    public DateTime ScheduledLocal => ScheduledAtUtc.ToLocalTime();

    [JsonIgnore]
    public string DateDisplay => ScheduledLocal.ToString("ddd, MMM d, yyyy");

    [JsonIgnore]
    public string TimeDisplay => ScheduledLocal.ToString("t");

    [JsonIgnore]
    public AppointmentStatusDto ParsedStatus =>
        Enum.TryParse<AppointmentStatusDto>(Status, true, out var s) ? s : AppointmentStatusDto.Pending;

    [JsonIgnore]
    public bool IsUpcomingActionable =>
        ScheduledLocal > DateTime.Now
        && ParsedStatus is not AppointmentStatusDto.Cancelled and not AppointmentStatusDto.Completed;
}

public enum AppointmentStatusDto
{
    Pending,
    Confirmed,
    Cancelled,
    Completed
}
