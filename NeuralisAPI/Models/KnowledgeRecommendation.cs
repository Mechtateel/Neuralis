using System.ComponentModel.DataAnnotations;

namespace NeuralisAPI.Models;

/// <summary>
/// Recommendation paragraph template for symptom NLP output (PostgreSQL knowledge base).
/// Mapped to table <c>Recommendations</c>.
/// </summary>
public class KnowledgeRecommendation
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>any | low | medium | high</summary>
    [MaxLength(20)]
    public string RiskLevel { get; set; } = "any";

    /// <summary>always | no_symptoms | has_conditions | high_risk | medium_risk</summary>
    [MaxLength(40)]
    public string ContextKey { get; set; } = "always";

    [MaxLength(2000)]
    public string Text { get; set; } = null!;

    public int SortOrder { get; set; }
}
