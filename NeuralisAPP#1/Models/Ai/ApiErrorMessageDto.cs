using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Ai;

public class ApiErrorMessageDto
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
