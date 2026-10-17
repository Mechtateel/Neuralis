namespace NeuralisAPI.Contracts.Common;

/// <summary>
/// News article shape returned by the public + admin News endpoints.
/// </summary>
public class NewsResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public string Subtitle { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Content { get; set; } = null!;
    public string Category { get; set; } = "general";
    public string Tags { get; set; } = "";
    public string CoverImageUrl { get; set; } = "";
    public string AuthorName { get; set; } = "Neuralis Admin";
    public Guid? AuthorId { get; set; }

    /// <summary>0 = Draft, 1 = Published, 2 = Archived.</summary>
    public int Status { get; set; }

    /// <summary>0 = Normal, 1 = Important, 2 = Critical.</summary>
    public int Priority { get; set; }

    public bool IsFeatured { get; set; }

    /// <summary>Legacy mirror of (Status == Published).</summary>
    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public int Views { get; set; }
}
