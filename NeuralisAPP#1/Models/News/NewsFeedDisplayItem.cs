namespace NeuralisAPP_1.Models.News;

/// <summary>UI-ready news row derived from API DTO (category and imagery are client-side for presentation).</summary>
public sealed class NewsFeedDisplayItem
{
    public required NewsItemDto Dto { get; init; }

    public string Title => Dto.Title;

    public string Content => Dto.Content;

    public string DateDisplay => Dto.CreatedLocalDisplay;

    public string CategoryLabel { get; init; } = string.Empty;

    public NewsCategoryKind Category { get; init; }

    /// <summary>MauiImage name under Resources/Images.</summary>
    public string HeroImage { get; init; } = "neuralis.png";

    /// <summary>Material Icons ligature for category.</summary>
    public string CategoryIcon { get; init; } = "article";

    public int ReadMinutes { get; init; } = 1;

    public string ExcerptCard { get; init; } = string.Empty;

    public string ExcerptFeatured { get; init; } = string.Empty;

    /// <summary>True for bundled demo content when the API returns no articles.</summary>
    public bool IsEditorialPlaceholder { get; init; }

    /// <summary>Shown when a category filter matches nothing but other articles exist.</summary>
    public bool IsFilterHint { get; init; }
}

public enum NewsCategoryKind
{
    AiResearch,
    ClinicalTrials,
    HealthTips,
    BioTech
}

public enum NewsCategoryFilter
{
    All,
    AiResearch,
    ClinicalTrials,
    HealthTips,
    BioTech
}
