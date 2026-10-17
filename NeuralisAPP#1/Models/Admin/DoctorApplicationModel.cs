namespace NeuralisAPP_1.Models.Admin;

public class DoctorApplicationModel
{
    public Guid DoctorId { get; set; }
    public Guid UserId { get; set; }

    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Specialization { get; set; } = "";
    public string LicenseNumber { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    // 🔹 UI helpers
    public string CreatedAtText => CreatedAt.ToString("dd MMM yyyy");

    public bool IsPending { get; set; } = true;
}
