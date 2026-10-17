using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.Lab;

public class LabStatsDto
{
    [JsonPropertyName("processedToday")]
    public int ProcessedToday { get; set; }
}
