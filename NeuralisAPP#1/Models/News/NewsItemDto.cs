using System.Text.Json.Serialization;

namespace NeuralisAPP_1.Models.News;

/// <summary>
/// News article DTO transferred over the wire.
/// All "CMS" fields (subtitle, summary, category, cover, status, …) default to safe
/// empty values so legacy payloads that only supplied (Id, Title, Content, CreatedAt)
/// keep deserializing correctly.
/// </summary>
public class NewsItemDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("subtitle")]
    public string Subtitle { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = "general";

    [JsonPropertyName("tags")]
    public string Tags { get; set; } = string.Empty;

    [JsonPropertyName("coverImageUrl")]
    public string CoverImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("authorName")]
    public string AuthorName { get; set; } = "Neuralis Admin";

    [JsonPropertyName("authorId")]
    public Guid? AuthorId { get; set; }

    /// <summary>0 = Draft, 1 = Published, 2 = Archived.</summary>
    [JsonPropertyName("status")]
    public int Status { get; set; } = 1;

    /// <summary>0 = Normal, 1 = Important, 2 = Critical.</summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;

    [JsonPropertyName("isFeatured")]
    public bool IsFeatured { get; set; }

    [JsonPropertyName("isPublished")]
    public bool IsPublished { get; set; } = true;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("views")]
    public int Views { get; set; }

    [JsonIgnore]
    public string CreatedLocalDisplay =>
        (PublishedAt ?? CreatedAt).ToLocalTime().ToString("g");

    [JsonIgnore]
    public string StatusDisplay => Status switch
    {
        0 => "Draft",
        2 => "Archived",
        _ => "Published",
    };
}
