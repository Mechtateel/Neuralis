using System.Globalization;
using System.Text;

namespace NeuralisAPI.Services.Lab;

public sealed class InterpretedLabRow
{
    public required string Name { get; init; }

    public required string ValueDisplay { get; init; }

    /// <summary>CRITICAL_LOW | LOW | NORMAL | HIGH (English). From value vs document ref only.</summary>
    public required string Status { get; init; }
}

public sealed class LabAnalysisNarrative
{
    public required string Summary { get; init; }

    public required string Priority { get; init; }

    public required List<string> Findings { get; init; }

    public required List<string> Recommendations { get; init; }
}

/// <summary>Deterministic interpretation: compare numeric value to min/max from the same table row only.</summary>
public sealed class LabInterpretationService
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public IReadOnlyList<InterpretedLabRow> InterpretRows(IReadOnlyList<LabValue> values, LabReferenceRanges.SexHint sex = LabReferenceRanges.SexHint.Unknown)
    {
        _ = sex;
        if (values.Count == 0)
            return Array.Empty<InterpretedLabRow>();

        var rows = new List<InterpretedLabRow>(values.Count);
        foreach (var v in values)
            rows.Add(InterpretOne(v));
        return rows;
    }

    /// <summary>
    /// Doctor-facing list: if any LOW/HIGH exists, return only those rows in order LOW then HIGH (document order within each).
    /// If all NORMAL, return the full list.
    /// </summary>
    public (IReadOnlyList<LabValue> Values, IReadOnlyList<InterpretedLabRow> Rows) PrepareDoctorFacing(IReadOnlyList<LabValue> values)
    {
        if (values.Count == 0)
            return (Array.Empty<LabValue>(), Array.Empty<InterpretedLabRow>());

        var rows = values.Select(InterpretOne).ToList();
        var pairs = values.Zip(rows, (v, r) => (v, r)).ToList();
        var abnormal = pairs.Where(p => p.r.Status != "NORMAL").ToList();
        if (abnormal.Count == 0)
            return (values, rows);

        var criticalLows = abnormal.Where(p => p.r.Status == "CRITICAL_LOW").ToList();
        var lows = abnormal.Where(p => p.r.Status == "LOW").ToList();
        var highs = abnormal.Where(p => p.r.Status == "HIGH").ToList();
        var ordered = criticalLows.Concat(lows).Concat(highs).ToList();
        return (
            ordered.Select(p => p.v).ToList(),
            ordered.Select(p => p.r).ToList());
    }

    public LabAnalysisNarrative BuildNarrative(IReadOnlyList<LabValue> values, LabReferenceRanges.SexHint sex = LabReferenceRanges.SexHint.Unknown)
    {
        _ = sex;
        if (values.Count == 0)
        {
            return new LabAnalysisNarrative
            {
                Summary = "No lab values extracted.",
                Priority = "LOW",
                Findings = new List<string>(),
                Recommendations = new List<string>
                {
                    "No measurements matched. Use a clearer PDF or OCR-friendly scan; the parser accepts numeric and qualitative row results when table structure is detected."
                }
            };
        }

        var rowsAll = values.Select(InterpretOne).ToList();
        var anyCriticalLow = rowsAll.Exists(r => r.Status == "CRITICAL_LOW");
        var anyLow = rowsAll.Exists(r => r.Status == "LOW");
        var anyHigh = rowsAll.Exists(r => r.Status == "HIGH");
        var anyAbnormal = anyCriticalLow || anyLow || anyHigh;

        var findings = new List<string>(values.Count);
        foreach (var (v, r) in values.Zip(rowsAll, (v, r) => (v, r)))
            findings.Add(FormatClinicalLine(v, r));

        string summary;
        if (!anyAbnormal)
            summary = "All lab values are within normal range.";
        else
        {
            var abnormalPairs = values.Zip(rowsAll, (v, r) => (v, r)).Where(t => t.r.Status is "CRITICAL_LOW" or "LOW" or "HIGH");
            var parts = abnormalPairs.Select(p => $"{p.v.Name} {p.r.Status.ToUpperInvariant()}");
            summary = "Abnormal findings detected: " + string.Join(", ", parts);
        }

        var priority = (anyCriticalLow || anyLow) ? "HIGH" : anyHigh ? "MEDIUM" : "LOW";

        var recommendations = new List<string>(1);
        if (anyAbnormal)
            recommendations.Add("Correlate with clinical context; repeat abnormal tests per protocol.");
        else
            recommendations.Add("No laboratory follow-up required based on these intervals.");

        return new LabAnalysisNarrative
        {
            Summary = summary,
            Priority = priority,
            Findings = findings,
            Recommendations = recommendations
        };
    }

    private static string FormatClinicalLine(LabValue v, InterpretedLabRow r)
    {
        var valUnit = string.Equals(v.ValueType, "qualitative", StringComparison.OrdinalIgnoreCase)
            ? (v.TextValue ?? string.Empty).Trim()
            : $"{v.Value.ToString(Inv)} {v.Unit}".Trim();
        return $"{v.Name} — {valUnit} — {r.Status.ToUpperInvariant()}";
    }

    private static InterpretedLabRow InterpretOne(LabValue v)
    {
        var status = LabResultClassifier.Classify(v);

        var display = string.Equals(v.ValueType, "qualitative", StringComparison.OrdinalIgnoreCase)
            ? (v.TextValue ?? string.Empty).Trim()
            : v.HasReferenceRange
                ? $"{v.Value.ToString(Inv)} {v.Unit} (ref {v.RefMin.ToString(Inv)}–{v.RefMax.ToString(Inv)})".Trim()
                : $"{v.Value.ToString(Inv)} {v.Unit}".Trim();
        return new InterpretedLabRow
        {
            Name = v.Name,
            ValueDisplay = display,
            Status = status
        };
    }
}
