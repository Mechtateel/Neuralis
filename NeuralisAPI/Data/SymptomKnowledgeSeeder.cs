using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Models;

namespace NeuralisAPI.Data;

/// <summary>
/// Seeds the symptom NLP knowledge base (migrated from ai-service hardcoded dictionaries).
/// Idempotent: upserts by symptom name and recommendation sort order.
/// </summary>
public static class SymptomKnowledgeSeeder
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedSymptomsAsync(db);
        await SeedRecommendationsAsync(db);
    }

    private static async Task SeedSymptomsAsync(AppDbContext db)
    {
        var entries = new (string Name, string[] Phrases, string[] Conditions, bool IsHighRisk, int Sort)[]
        {
            ("shortness of breath", ["shortness of breath"], _SYMPTOM_TO_CONDITIONS["shortness of breath"], true, 1),
            ("chest pain", ["chest pain"], _SYMPTOM_TO_CONDITIONS["chest pain"], true, 2),
            ("sore throat", ["sore throat", "throat pain"], _SYMPTOM_TO_CONDITIONS["sore throat"], false, 3),
            ("runny nose", ["runny nose"], _SYMPTOM_TO_CONDITIONS["runny nose"], false, 4),
            ("muscle aches", ["muscle ache", "muscle aches", "body aches"], _SYMPTOM_TO_CONDITIONS["muscle aches"], false, 5),
            ("stomach pain", ["stomach ache", "abdominal pain"], _SYMPTOM_TO_CONDITIONS["stomach pain"], false, 6),
            ("loss of appetite", ["loss of appetite"], _SYMPTOM_TO_CONDITIONS["loss of appetite"], false, 7),
            ("night sweats", ["night sweats"], _SYMPTOM_TO_CONDITIONS["night sweats"], false, 8),
            ("fever", ["high fever", "low grade fever", "fever", "temperature"], _SYMPTOM_TO_CONDITIONS["fever"], false, 9),
            ("fatigue", ["feeling tired", "fatigue", "tired"], _SYMPTOM_TO_CONDITIONS["fatigue"], false, 10),
            ("headache", ["headache", "migraine"], _SYMPTOM_TO_CONDITIONS["headache"], false, 11),
            ("chills", ["chills"], _SYMPTOM_TO_CONDITIONS["chills"], false, 12),
            ("cough", ["cough"], _SYMPTOM_TO_CONDITIONS["cough"], false, 13),
            ("nausea", ["nausea"], _SYMPTOM_TO_CONDITIONS["nausea"], false, 14),
            ("vomiting", ["vomiting"], _SYMPTOM_TO_CONDITIONS["vomiting"], false, 15),
            ("diarrhea", ["diarrhea"], _SYMPTOM_TO_CONDITIONS["diarrhea"], false, 16),
            ("dizziness", ["dizziness"], _SYMPTOM_TO_CONDITIONS["dizziness"], false, 17),
            ("rash", ["rash"], _SYMPTOM_TO_CONDITIONS["rash"], false, 18),
            ("wheezing", ["wheeze", "wheezing"], _SYMPTOM_TO_CONDITIONS["wheezing"], false, 19),
            ("bleeding", ["bleeding"], _SYMPTOM_TO_CONDITIONS["bleeding"], true, 20),
            ("severe pain", ["severe pain"], _SYMPTOM_TO_CONDITIONS["severe pain"], true, 21),
        };

        foreach (var (name, phrases, conditions, isHighRisk, sort) in entries)
        {
            var existing = await db.Symptoms.FirstOrDefaultAsync(s => s.Name == name);
            var phrasesJson = JsonSerializer.Serialize(phrases, JsonOpts);
            var conditionsJson = JsonSerializer.Serialize(conditions, JsonOpts);

            if (existing == null)
            {
                db.Symptoms.Add(new Symptom
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    MatchPhrasesJson = phrasesJson,
                    PossibleConditionsJson = conditionsJson,
                    IsHighRisk = isHighRisk,
                    SortOrder = sort,
                });
            }
            else
            {
                existing.MatchPhrasesJson = phrasesJson;
                existing.PossibleConditionsJson = conditionsJson;
                existing.IsHighRisk = isHighRisk;
                existing.SortOrder = sort;
            }
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedRecommendationsAsync(AppDbContext db)
    {
        var templates = new (string RiskLevel, string ContextKey, string Text, int Sort)[]
        {
            ("any", "always",
                "This output lists **possible conditions** only for educational purposes.", 1),
            ("any", "always",
                "It is **not a medical diagnosis** and cannot replace professional assessment.", 2),
            ("any", "always",
                "Please **consult a doctor** or appropriate clinician if you are unwell, if symptoms are new or worsening, or if you are unsure.", 3),
            ("high", "high_risk",
                "Some wording you used can match symptoms that may need **prompt in-person or emergency care** — when in doubt, seek medical attention.", 4),
            ("medium", "medium_risk",
                "Consider scheduling a routine medical visit if symptoms persist beyond a few days or interfere with daily activities.", 5),
            ("any", "no_symptoms",
                "No specific symptom keywords were detected; sharing more detail with a clinician will help them advise you.", 6),
            ("any", "has_conditions",
                "Discuss any items that concern you with a qualified professional — they can interpret them in your full context.", 7),
        };

        foreach (var (risk, context, text, sort) in templates)
        {
            var existing = await db.Recommendations
                .FirstOrDefaultAsync(r => r.SortOrder == sort && r.ContextKey == context);

            if (existing == null)
            {
                db.Recommendations.Add(new KnowledgeRecommendation
                {
                    Id = Guid.NewGuid(),
                    RiskLevel = risk,
                    ContextKey = context,
                    Text = text,
                    SortOrder = sort,
                });
            }
            else
            {
                existing.RiskLevel = risk;
                existing.Text = text;
            }
        }

        await db.SaveChangesAsync();
    }

    private static readonly Dictionary<string, string[]> _SYMPTOM_TO_CONDITIONS = new(StringComparer.Ordinal)
    {
        ["headache"] = ["Tension-type headache (possible)", "Migraine (possible)"],
        ["fever"] = ["Viral illness (possible)", "Other causes of fever (possible)"],
        ["chills"] = ["Febrile illness (possible)", "Viral or bacterial process (possible)"],
        ["cough"] = ["Upper airway irritation (possible)", "Common cold / viral URI (possible)"],
        ["sore throat"] = ["Viral pharyngitis (possible)", "Other throat irritation (possible)"],
        ["runny nose"] = ["Allergic rhinitis (possible)", "Viral upper respiratory infection (possible)"],
        ["shortness of breath"] = ["Asthma exacerbation (possible)", "Other cardiopulmonary causes (possible)"],
        ["chest pain"] = ["Musculoskeletal chest wall pain (possible)", "Other causes needing evaluation (possible)"],
        ["nausea"] = ["Gastroenteritis (possible)", "Other gastrointestinal upset (possible)"],
        ["vomiting"] = ["Gastroenteritis (possible)", "Dehydration risk — seek care if persistent (possible)"],
        ["diarrhea"] = ["Gastroenteritis (possible)", "Dietary intolerance (possible)"],
        ["fatigue"] = ["Viral illness recovery (possible)", "Sleep deprivation or stress (possible)"],
        ["muscle aches"] = ["Viral syndrome (possible)", "Overexertion (possible)"],
        ["dizziness"] = ["Dehydration (possible)", "Inner ear issues (possible)"],
        ["rash"] = ["Dermatitis (possible)", "Viral exanthem (possible)"],
        ["wheezing"] = ["Reactive airways (possible)", "Asthma-like symptoms (possible)"],
        ["stomach pain"] = ["Gastritis / indigestion (possible)", "Other abdominal causes (possible)"],
        ["loss of appetite"] = ["Mild viral illness (possible)", "Many non-specific causes (possible)"],
        ["night sweats"] = ["Infection or other causes (possible)", "Needs clinician review if ongoing (possible)"],
        ["bleeding"] = ["Local injury (possible)", "Requires prompt medical attention if significant"],
        ["severe pain"] = ["Many causes (possible)", "Seek urgent medical advice if severe or worsening"],
    };
}
