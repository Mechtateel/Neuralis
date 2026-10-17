namespace NeuralisAPP_1.Models.Doctor;

public static class DoctorSpecializationUi
{
    public const string LabTechnician = "LabTechnician";

    public static bool IsLabTechnician(string? specialization) =>
        string.Equals(specialization, LabTechnician, StringComparison.OrdinalIgnoreCase);

    /// <summary>Human-readable label for API specialization codes.</summary>
    public static string Format(string? code) =>
        code switch
        {
            null or "" => "",
            "ENT" => "ENT (Otolaryngologist)",
            "Urologist" => "Urologist (😏 Specialist)",
            "Proctologist" => "Proctologist (💀 Dangerous Doctor)",
            _ => code
        };
}
