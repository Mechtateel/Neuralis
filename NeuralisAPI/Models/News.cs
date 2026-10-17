using System.ComponentModel.DataAnnotations;

namespace NeuralisAPI.Models;

/// <summary>
/// Editorial article published by an admin to the Neuralis patient feed.
/// New CMS-style fields (subtitle, summary, category, tags, cover, status, priority, author,
/// publish time, views) were added on top of the original (Id, Title, Content, CreatedAt,
/// IsPublished) shape so existing consumers continue to work without changes.
/// </summary>
public class News
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = null!;

    /// <summary>Short headline shown under the title on the patient feed.</summary>
    [MaxLength(300)]
    public string Subtitle { get; set; } = "";

    /// <summary>Card-level excerpt (manual). Falls back to Content[..n] when empty.</summary>
    [MaxLength(1000)]
    public string Summary { get; set; } = "";

    [MaxLength(4000)]
    public string Content { get; set; } = null!;

    /// <summary>
    /// Category slug: <c>ai_research</c>, <c>clinical_trials</c>, <c>health_tips</c>,
    /// <c>biotech</c>, <c>announcement</c>, <c>advisory</c>, <c>general</c>.
    /// Kept as string so admins can grow the taxonomy without DB migrations.
    /// </summary>
    [MaxLength(60)]
    public string Category { get; set; } = "general";

    /// <summary>Comma-separated tag list.</summary>
    [MaxLength(500)]
    public string Tags { get; set; } = "";

    /// <summary>
    /// Either a resource name bundled with the app (e.g. <c>neuralis.png</c>) or an external URL.
    /// Empty means the patient feed should pick a default hero image.
    /// </summary>
    [MaxLength(1000)]
    public string CoverImageUrl { get; set; } = "";

    [MaxLength(200)]
    public string AuthorName { get; set; } = "Neuralis Admin";

    public Guid? AuthorId { get; set; }

    /// <summary>0 = Draft, 1 = Published, 2 = Archived.</summary>
    public int Status { get; set; } = (int)NewsStatus.Published;

    /// <summary>0 = Normal, 1 = Important, 2 = Critical / pinned alert.</summary>
    public int Priority { get; set; } = 0;

    public bool IsFeatured { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PublishedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int Views { get; set; }

    /// <summary>
    /// Legacy compatibility column — kept so the original DTO contract continues to compile.
    /// Mirrors <c>Status == Published</c> on writes by the controller.
    /// </summary>
    public bool IsPublished { get; set; } = true;
}

public enum NewsStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}

public enum NewsPriority
{
    Normal = 0,
    Important = 1,
    Critical = 2,
}
