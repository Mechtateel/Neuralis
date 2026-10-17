namespace NeuralisAPP_1.ViewModels.Lab;

public sealed class PatientDirectoryRowViewModel
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string IdentityLine { get; init; } = string.Empty;
    public string Initials { get; init; } = "NA";
    public string StatusLabel { get; init; } = string.Empty;
    public string StatusBackground { get; init; } = "#1f2937";
    public string StatusForeground { get; init; } = "#cbd5e1";
    public string LastSyncRelative { get; init; } = string.Empty;
}
