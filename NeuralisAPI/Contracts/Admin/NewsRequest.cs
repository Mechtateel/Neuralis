using System.ComponentModel.DataAnnotations;

namespace NeuralisAPI.Contracts.Admin;

/// <summary>
/// Payload used by the admin News editor. Older clients can still POST with just
/// (Title, Content, IsPublished) — every CMS field has a safe default.
/// </summary>
public class NewsRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = null!;

    [MaxLength(300)]
    public string Subtitle { get; set; } = "";

    [MaxLength(1000)]
    public string Summary { get; set; } = "";

    [MaxLength(60)]
    public string Category { get; set; } = "general";

    [MaxLength(500)]
    public string Tags { get; set; } = "";

    [MaxLength(1000)]
    public string CoverImageUrl { get; set; } = "";

    /// <summary>0 = Draft, 1 = Published, 2 = Archived. When provided, supersedes <see cref="IsPublished"/>.</summary>
    public int? Status { get; set; }

    /// <summary>0 = Normal, 1 = Important, 2 = Critical.</summary>
    public int Priority { get; set; } = 0;

    public bool IsFeatured { get; set; }

    /// <summary>Optional scheduled publish time. If null and Status=Published, defaults to UtcNow.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Legacy compatibility — old admin clients only set this boolean.</summary>
    public bool IsPublished { get; set; } = true;
}
