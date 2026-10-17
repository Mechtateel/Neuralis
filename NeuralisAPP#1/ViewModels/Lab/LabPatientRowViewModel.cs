using NeuralisAPP_1.Models.Lab;

namespace NeuralisAPP_1.ViewModels.Lab;

public sealed class LabPatientRowViewModel
{
    public Guid PatientUserId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public bool HasUploadedResults { get; init; }

    public string PatientIdLine => $"Patient ID: {PatientUserId:D}";

    public string EmailLine =>
        string.IsNullOrWhiteSpace(Email) ? "No email on file" : Email;

    public string StatusText => HasUploadedResults ? "RESULTS UPLOADED" : "WAITING FOR LAB";

    public Color StatusColor => HasUploadedResults
        ? Color.FromArgb("#34D399")
        : Color.FromArgb("#EAB308");

    public bool IsWaiting => !HasUploadedResults;

    public Color CardStroke => Color.FromArgb("#1a3338");

    public double CardStrokeThickness => 1;

    public static LabPatientRowViewModel FromDto(LabAssignedPatientDto dto)
    {
        var name = string.IsNullOrWhiteSpace(dto.FullName) ? dto.Email : dto.FullName;

        return new LabPatientRowViewModel
        {
            PatientUserId = dto.PatientUserId,
            FullName = name,
            Email = dto.Email,
            HasUploadedResults = dto.HasUploadedResults
        };
    }
}
