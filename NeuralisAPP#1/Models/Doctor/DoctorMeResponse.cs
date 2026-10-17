using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Doctor;

public class DoctorMeResponse
{
    [JsonPropertyName("doctorId")]
    public Guid DoctorId { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("specialization")]
    public string Specialization { get; set; } = string.Empty;

    [JsonPropertyName("isVerified")]
    public bool IsVerified { get; set; }
}
