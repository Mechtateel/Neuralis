namespace NeuralisAPI.Models;

/// <summary>Canonical specialization values stored in <see cref="DoctorProfile.Specialization"/>.</summary>
public static class DoctorSpecializations
{
    public const string Therapist = "Therapist";
    public const string Ent = "ENT";
    public const string Dermatologist = "Dermatologist";
    public const string LabTechnician = "LabTechnician";
    public const string Urologist = "Urologist";
    public const string Proctologist = "Proctologist";

    public static readonly string[] All =
    {
        Therapist,
        Ent,
        Dermatologist,
        LabTechnician,
        Urologist,
        Proctologist
    };

    public static bool IsLabTechnician(string? specialization) =>
        string.Equals(specialization, LabTechnician, StringComparison.OrdinalIgnoreCase);

    /// <summary>Doctors who can receive patient appointments.</summary>
    public static bool CanAcceptAppointments(string? specialization) =>
        !string.IsNullOrWhiteSpace(specialization) && !IsLabTechnician(specialization);
}
