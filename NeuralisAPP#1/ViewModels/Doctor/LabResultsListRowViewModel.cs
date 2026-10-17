using NeuralisAPP_1.Models.Lab;

namespace NeuralisAPP_1.ViewModels.Doctor;

public sealed class LabResultsListRowViewModel
{
    public Guid Id { get; init; }

    public string PatientDisplay { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string UploadedDisplay { get; init; } = string.Empty;

    public static LabResultsListRowViewModel FromDto(LabResultListItemDto d)
    {
        var name = string.IsNullOrWhiteSpace(d.PatientFullName) ? d.PatientEmail : d.PatientFullName;
        return new LabResultsListRowViewModel
        {
            Id = d.Id,
            PatientDisplay = string.IsNullOrWhiteSpace(name) ? d.PatientUserId.ToString("D") : name,
            FileName = d.FileName,
            UploadedDisplay = d.UploadedAtUtc.ToLocalTime().ToString("g")
        };
    }
}
