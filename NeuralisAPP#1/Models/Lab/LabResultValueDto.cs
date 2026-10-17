using System.Text.Json;
using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Lab;

public class LabResultValueDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public JsonElement Value { get; set; }

    [JsonPropertyName("numericValue")]
    public double? NumericValue { get; set; }

    [JsonPropertyName("textValue")]
    public string? TextValue { get; set; }

    [JsonPropertyName("booleanValue")]
    public bool? BooleanValue { get; set; }

    [JsonPropertyName("resultKind")]
    public string? ResultKind { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("refMin")]
    public double? RefMin { get; set; }

    [JsonPropertyName("refMax")]
    public double? RefMax { get; set; }

    [JsonPropertyName("section")]
    public string? Section { get; set; }

    [JsonPropertyName("lowConfidence")]
    public bool? LowConfidence { get; set; }
}
