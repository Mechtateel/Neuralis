using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Auth;

public class MeResponse
{
    [JsonPropertyName("userId")]
    public Guid Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("role")]
    public int Role { get; set; }

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }

    /// <summary>Set for doctors from <c>/api/auth/me</c>; matches <see cref="DoctorProfile"/> specialization code.</summary>
    [JsonPropertyName("doctorSpecialization")]
    public string? DoctorSpecialization { get; set; }
}
