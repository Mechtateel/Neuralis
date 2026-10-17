using System.ComponentModel.DataAnnotations;

namespace NeuralisAPI.Models;

/// <summary>
/// Canonical symptom entry for rule-based NLP matching (PostgreSQL knowledge base).
/// <see cref="MatchPhrasesJson"/> and <see cref="PossibleConditionsJson"/> are JSON string arrays.
/// </summary>
public class Symptom
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = null!;

    /// <summary>JSON array of lowercase phrases mapped to <see cref="Name"/>.</summary>
    public string MatchPhrasesJson { get; set; } = "[]";

    /// <summary>JSON array of educational possible-condition labels (not diagnoses).</summary>
    public string PossibleConditionsJson { get; set; } = "[]";

    public bool IsHighRisk { get; set; }

    public int SortOrder { get; set; }
}
