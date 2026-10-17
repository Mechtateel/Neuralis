using System.Text.RegularExpressions;
using NeuralisAPI.Contracts.Ai;
using NeuralisAPI.Models;

namespace NeuralisAPI.Services;

/// <summary>
/// Rule-based symptom NLP using PostgreSQL knowledge base (replaces ai-service hardcoded dictionaries).
/// </summary>
public sealed class TextAnalysisService
{
    private static readonly HashSet<string> TokenFallbacks = new(StringComparer.Ordinal)
    {
        "headache", "fever", "cough", "nausea", "fatigue", "chills", "rash",
    };

    private readonly SymptomKnowledgeService _knowledge;

    public TextAnalysisService(SymptomKnowledgeService knowledge)
    {
        _knowledge = knowledge;
    }

    public async Task<AnalyzeTextAnalysisResponse> AnalyzeAsync(string text, CancellationToken cancellationToken = default)
    {
        var kb = await _knowledge.GetSnapshotAsync(cancellationToken);
        var normalized = NormalizeText(text);
        var symptoms = ExtractSymptoms(normalized, kb);
        var possibleConditions = MapSymptomsToConditions(symptoms, kb);
        var riskLevel = ComputeRiskLevel(symptoms, normalized, kb);
        var recommendation = BuildRecommendation(symptoms, possibleConditions, riskLevel, kb);

        return new AnalyzeTextAnalysisResponse
        {
            Symptoms = symptoms,
            PossibleConditions = possibleConditions,
            Recommendation = recommendation,
            RiskLevel = riskLevel,
            Confidence = ConfidenceFromRisk(riskLevel),
            AiInterpretation = "AI interpretation unavailable.",
        };
    }

    private static string NormalizeText(string raw) =>
        Regex.Replace(raw.Trim().ToLowerInvariant(), @"\s+", " ");

    private static List<string> ExtractSymptoms(string normalized, SymptomKnowledgeSnapshot kb)
    {
        if (string.IsNullOrEmpty(normalized))
            return [];

        var found = new LinkedList<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddSymptom(string name)
        {
            if (seen.Add(name))
                found.AddLast(name);
        }

        foreach (var rule in kb.PhraseRules)
        {
            if (normalized.Contains(rule.Phrase, StringComparison.Ordinal))
                AddSymptom(rule.SymptomName);
        }

        foreach (var token in normalized.Split(',', ';'))
        {
            var t = token.Trim();
            if (string.IsNullOrEmpty(t))
                continue;

            var matched = false;
            foreach (var rule in kb.PhraseRules)
            {
                if (rule.Phrase == t || (t.Length > 2 && t == rule.Phrase))
                {
                    AddSymptom(rule.SymptomName);
                    matched = true;
                    break;
                }
            }

            if (!matched && TokenFallbacks.Contains(t))
                AddSymptom(t);
        }

        return found.ToList();
    }

    private static List<string> MapSymptomsToConditions(List<string> symptoms, SymptomKnowledgeSnapshot kb)
    {
        var outList = new LinkedList<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var symptom in symptoms)
        {
            if (!kb.SymptomConditions.TryGetValue(symptom, out var conds))
                continue;

            foreach (var cond in conds)
            {
                if (seen.Add(cond))
                    outList.AddLast(cond);
            }
        }

        if (outList.Count == 0 && symptoms.Count > 0)
            outList.AddLast("Non-specific symptoms (possible — discuss with a clinician)");

        return outList.ToList();
    }

    private static string ComputeRiskLevel(List<string> symptoms, string textLower, SymptomKnowledgeSnapshot kb)
    {
        if (symptoms.Any(s => kb.HighRiskSymptomNames.Contains(s)))
            return "high";

        if (textLower.Contains("can't breathe", StringComparison.Ordinal)
            || textLower.Contains("cannot breathe", StringComparison.Ordinal)
            || textLower.Contains("crushing chest", StringComparison.Ordinal)
            || textLower.Contains("coughing blood", StringComparison.Ordinal))
            return "high";

        if (symptoms.Contains("fever") && (symptoms.Contains("cough") || symptoms.Contains("sore throat")))
            return "medium";

        if (symptoms.Count >= 3)
            return "medium";

        return symptoms.Count > 0 ? "low" : "low";
    }

    private static double ConfidenceFromRisk(string riskLevel) => riskLevel switch
    {
        "low" => 30.0,
        "medium" => 60.0,
        "high" => 85.0,
        _ => 50.0,
    };

    private static string BuildRecommendation(
        List<string> symptoms,
        List<string> possibleConditions,
        string riskLevel,
        SymptomKnowledgeSnapshot kb)
    {
        var parts = new List<string>();

        foreach (var template in kb.RecommendationTemplates)
        {
            if (!ShouldIncludeTemplate(template, symptoms, possibleConditions, riskLevel))
                continue;

            parts.Add(template.Text);
        }

        return parts.Count == 0
            ? "Please consult a doctor if you are unwell. This output is not a medical diagnosis."
            : string.Join(' ', parts);
    }

    private static bool ShouldIncludeTemplate(
        KnowledgeRecommendation template,
        List<string> symptoms,
        List<string> possibleConditions,
        string riskLevel)
    {
        return template.ContextKey switch
        {
            "always" => true,
            "high_risk" => riskLevel == "high",
            "medium_risk" => riskLevel == "medium",
            "no_symptoms" => symptoms.Count == 0,
            "has_conditions" => possibleConditions.Count > 0,
            _ => false,
        };
    }
}
