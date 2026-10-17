namespace NeuralisAPI.Services.Lab;

/// <summary>Adult reference intervals for deterministic interpretation (sex unknown uses envelope min–max).</summary>
public static class LabReferenceRanges
{
    public enum SexHint
    {
        Unknown,
        Male,
        Female
    }

    public readonly record struct Interval(double Low, double High, string Unit, string Label);

    public static Interval Ferritin(SexHint sex) => sex switch
    {
        SexHint.Male => new Interval(30, 400, "ng/mL", "adult male"),
        SexHint.Female => new Interval(15, 150, "ng/mL", "adult female"),
        _ => new Interval(15, 400, "ng/mL", "adult (sex unknown; envelope)")
    };

    public static Interval Hemoglobin(SexHint sex) => sex switch
    {
        SexHint.Male => new Interval(13.5, 17.5, "g/dL", "adult male"),
        SexHint.Female => new Interval(12.0, 15.5, "g/dL", "adult female"),
        _ => new Interval(12.0, 17.5, "g/dL", "adult (sex unknown; envelope)")
    };

    /// <summary>Fasting plasma glucose (mg/dL).</summary>
    public static Interval GlucoseFasting() => new(70, 99, "mg/dL", "fasting adult");

    public static Interval Creatinine(SexHint sex) => sex switch
    {
        SexHint.Male => new Interval(0.74, 1.35, "mg/dL", "adult male"),
        SexHint.Female => new Interval(0.59, 1.04, "mg/dL", "adult female"),
        _ => new Interval(0.59, 1.35, "mg/dL", "adult (sex unknown; envelope)")
    };

    /// <summary>Thyroid-stimulating hormone (adult envelope).</summary>
    public static Interval Tsh() => new(0.4, 4.0, "mIU/L", "adult (typical lab)");

    /// <summary>C-reactive protein (high-sensitivity context varies; wide envelope).</summary>
    public static Interval Crp() => new(0, 3, "mg/L", "adult (hs-CRP style envelope)");
}
