using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Lab;

public class LabResultDetailDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("testName")]
    public string TestName { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("uploadedAtUtc")]
    public DateTime UploadedAtUtc { get; set; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    [JsonPropertyName("values")]
    public List<LabResultValueDto>? Values { get; set; }
}
