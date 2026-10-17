using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NeuralisAPI.Data;
using NeuralisAPI.Models;

namespace NeuralisAPI.Services;

public sealed record SymptomPhraseRule(string Phrase, string SymptomName, bool IsHighRisk);

public sealed record SymptomKnowledgeSnapshot(
    IReadOnlyList<SymptomPhraseRule> PhraseRules,
    IReadOnlyDictionary<string, IReadOnlyList<string>> SymptomConditions,
    IReadOnlySet<string> HighRiskSymptomNames,
    IReadOnlyList<KnowledgeRecommendation> RecommendationTemplates);

/// <summary>Loads and caches symptom/recommendation knowledge base rows from PostgreSQL.</summary>
public sealed class SymptomKnowledgeService
{
    private const string CacheKey = "symptom_knowledge_snapshot_v1";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SymptomKnowledgeService> _logger;

    public SymptomKnowledgeService(
        AppDbContext db,
        IMemoryCache cache,
        ILogger<SymptomKnowledgeService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SymptomKnowledgeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out SymptomKnowledgeSnapshot? cached) && cached != null)
            return cached;

        var snapshot = await LoadSnapshotAsync(cancellationToken);
        _cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    private async Task<SymptomKnowledgeSnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        var symptoms = await _db.Symptoms.AsNoTracking().OrderBy(s => s.SortOrder).ToListAsync(cancellationToken);
        var recommendations = await _db.Recommendations.AsNoTracking().OrderBy(r => r.SortOrder).ToListAsync(cancellationToken);

        if (symptoms.Count == 0)
            _logger.LogWarning("Symptom knowledge base is empty — run SymptomKnowledgeSeeder on startup");

        var phraseRules = new List<SymptomPhraseRule>();
        var conditions = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var highRisk = new HashSet<string>(StringComparer.Ordinal);

        foreach (var symptom in symptoms)
        {
            if (symptom.IsHighRisk)
                highRisk.Add(symptom.Name);

            conditions[symptom.Name] = ParseJsonArray(symptom.PossibleConditionsJson);

            foreach (var phrase in ParseJsonArray(symptom.MatchPhrasesJson))
            {
                if (!string.IsNullOrWhiteSpace(phrase))
                    phraseRules.Add(new SymptomPhraseRule(phrase.Trim().ToLowerInvariant(), symptom.Name, symptom.IsHighRisk));
            }
        }

        phraseRules.Sort((a, b) => b.Phrase.Length.CompareTo(a.Phrase.Length));

        return new SymptomKnowledgeSnapshot(
            phraseRules,
            conditions,
            highRisk,
            recommendations);
    }

    private static IReadOnlyList<string> ParseJsonArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
