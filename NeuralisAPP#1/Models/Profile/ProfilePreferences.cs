namespace NeuralisAPP_1.Models.Profile;

/// <summary>
/// Local profile preferences. Persisted via <see cref="Services.ProfilePreferencesService"/>.
/// Backend sync is currently a stub — the model is shaped so it can be POSTed to a future
/// <c>/api/profile/preferences</c> endpoint without further refactor.
/// </summary>
public class ProfilePreferences
{
    // Editable user fields (no backend endpoint yet — stored locally per user id)
    public string Phone { get; set; } = "";
    public string DateOfBirth { get; set; } = "";          // ISO yyyy-MM-dd, free-text fallback
    public string Gender { get; set; } = "";               // "", "Male", "Female", "Other", "Prefer not to say"
    public string EmergencyContact { get; set; } = "";
    public string Bio { get; set; } = "";
    public string DoctorClinic { get; set; } = "";

    /// <summary>
    /// Email change requested by the user. The canonical email lives on the server (<c>/auth/me</c>)
    /// and cannot be mutated locally yet — this field stores the pending value so the UI can show
    /// a "pending administrator review" state until the backend endpoint ships.
    /// </summary>
    public string PendingEmail { get; set; } = "";

    // Doctor availability (real toggle, persisted locally; surfaced on dashboard later)
    public bool IsAvailable { get; set; } = true;

    // Language code: "en" | "ru" | "kz"
    public string LanguageCode { get; set; } = "en";

    // Notifications
    public bool NotifyAiAnalysis { get; set; } = true;
    public bool NotifyAppointments { get; set; } = true;
    public bool NotifySecurity { get; set; } = true;
    public bool NotifySystem { get; set; } = false;

    // Privacy
    public bool ProfileVisible { get; set; } = true;
    public bool ShareAnalytics { get; set; } = false;
    public bool SaveAiHistory { get; set; } = true;
    public bool ActivityTracking { get; set; } = false;
}
