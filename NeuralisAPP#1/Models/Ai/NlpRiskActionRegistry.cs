using Microsoft.Maui.Graphics;

namespace NeuralisAPP_1.Models.Ai;

/// <summary>
/// Identifies a user-facing action triggered from the NLP "Recommended Action" card.
/// Add new enum values here when future risk tiers need different navigation targets.
/// </summary>
public enum NlpRiskActionKind
{
    LearnMore,
    BookAppointment,
    ContactDoctor,
    EmergencyCall
}

/// <summary>
/// Describes one button on the risk-based action card.
/// </summary>
public sealed class NlpRiskActionButton
{
    public required string Label { get; init; }
    public required NlpRiskActionKind Kind { get; init; }
    /// <summary>When true, uses PrimaryButton styling; otherwise GhostButton.</summary>
    public bool IsPrimary { get; init; }
}

/// <summary>
/// Visual and copy profile for a single NLP risk band.
/// Register additional keys in <see cref="NlpRiskActionRegistry"/> to support new levels
/// without changing page markup or layout logic.
/// </summary>
public sealed class NlpRiskActionProfile
{
    public required string RiskLevelKey { get; init; }
    public required Color AccentColor { get; init; }
    public required Color BackgroundColor { get; init; }
    public required Color BorderColor { get; init; }
    public double BorderThickness { get; init; } = 1;
    public bool ShowWarningIcon { get; init; }
    /// <summary>Educational guidance only — must not imply a diagnosis.</summary>
    public required string PrimaryMessage { get; init; }
    public string? SecondaryMessage { get; init; }
    public required IReadOnlyList<NlpRiskActionButton> Buttons { get; init; }
}

/// <summary>
/// Central lookup for risk-based recommended actions on the AI Analysis page.
/// UI code resolves a profile by normalized risk level and applies it generically.
/// </summary>
public static class NlpRiskActionRegistry
{
    // Palette aligned with AIAnalysisPage.ResolveRiskColor and existing status badges.
    private static readonly Color LowAccent = Color.FromArgb("#4ADE80");
    private static readonly Color MediumAccent = Color.FromArgb("#FACC15");
    private static readonly Color HighAccent = Color.FromArgb("#F87171");

    private static readonly IReadOnlyDictionary<string, NlpRiskActionProfile> Profiles =
        new Dictionary<string, NlpRiskActionProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["low"] = new NlpRiskActionProfile
            {
                RiskLevelKey = "low",
                AccentColor = LowAccent,
                BackgroundColor = Color.FromArgb("#0F2A1A"),
                BorderColor = LowAccent,
                PrimaryMessage = "Monitor your symptoms and follow general health recommendations.",
                Buttons = new[]
                {
                    new NlpRiskActionButton
                    {
                        Label = "Learn More",
                        Kind = NlpRiskActionKind.LearnMore,
                        IsPrimary = false
                    }
                }
            },
            ["medium"] = new NlpRiskActionProfile
            {
                RiskLevelKey = "medium",
                AccentColor = MediumAccent,
                BackgroundColor = Color.FromArgb("#2A2510"),
                BorderColor = MediumAccent,
                PrimaryMessage = "A consultation with a healthcare professional is recommended.",
                Buttons = new[]
                {
                    new NlpRiskActionButton
                    {
                        Label = "Book Appointment",
                        Kind = NlpRiskActionKind.BookAppointment,
                        IsPrimary = true
                    }
                }
            },
            ["high"] = new NlpRiskActionProfile
            {
                RiskLevelKey = "high",
                AccentColor = HighAccent,
                BackgroundColor = Color.FromArgb("#2A1010"),
                BorderColor = HighAccent,
                BorderThickness = 2,
                ShowWarningIcon = true,
                PrimaryMessage = "High-risk symptoms detected. Immediate medical attention is recommended.",
                SecondaryMessage = "If symptoms become severe, contact emergency services immediately.",
                Buttons = new[]
                {
                    new NlpRiskActionButton
                    {
                        Label = "Contact Doctor",
                        Kind = NlpRiskActionKind.ContactDoctor,
                        IsPrimary = true
                    },
                    new NlpRiskActionButton
                    {
                        Label = "Emergency Call",
                        Kind = NlpRiskActionKind.EmergencyCall,
                        IsPrimary = false
                    }
                }
            }
        };

    /// <summary>
    /// Normalizes API risk strings (e.g. "High", " HIGH ") to registry keys.
    /// </summary>
    public static string NormalizeRiskLevel(string? riskLevel)
    {
        if (string.IsNullOrWhiteSpace(riskLevel))
            return "low";

        return riskLevel.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Returns the action profile for a risk level, or the low-risk fallback for unknown tiers.
    /// </summary>
    public static NlpRiskActionProfile Resolve(string? riskLevel)
    {
        var key = NormalizeRiskLevel(riskLevel);
        return Profiles.TryGetValue(key, out var profile)
            ? profile
            : Profiles["low"];
    }
}
