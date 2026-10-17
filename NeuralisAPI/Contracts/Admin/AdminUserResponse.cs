namespace NeuralisAPI.Contracts.Admin;

public class AdminUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool IsBlocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? FullName { get; set; }
    public bool HasDoctorProfile { get; set; }
    public bool? IsDoctorVerified { get; set; }
}
