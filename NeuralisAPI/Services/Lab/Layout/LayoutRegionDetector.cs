using System.Text.RegularExpressions;

namespace NeuralisAPI.Services.Lab.Layout;

/// <summary>Assigns lines to header / body / footer using vertical position; flags obvious noise bands.</summary>
public static class LayoutRegionDetector
{
    private static readonly Regex EmailRx = new(@"\S+@\S+\.\S+", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PhoneRx = new(@"(?:\+\d[\d\s\-().]{8,}\d|\b\d{10,}\b)", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<LayoutRegion> Partition(LabPageLayout page, IReadOnlyList<LayoutLine> lines)
    {
        if (lines.Count == 0)
            return Array.Empty<LayoutRegion>();

        var ph = page.PageHeight <= 0 ? 1 : page.PageHeight;
        var regions = new List<LayoutRegion>();
        var header = new List<LayoutLine>();
        var body = new List<LayoutLine>();
        var footer = new List<LayoutLine>();
        var noise = new List<LayoutLine>();

        foreach (var line in lines)
        {
            var relTop = 1.0 - (line.MidY / ph);
            if (relTop < 0 || relTop > 1)
                relTop = 0.5;

            var plain = string.Join(' ', line.Words.Select(w => w.Text));
            if (LooksLikeNoiseLine(plain))
            {
                noise.Add(line);
                continue;
            }

            if (relTop < 0.11)
                header.Add(line);
            else if (relTop > 0.86)
                footer.Add(line);
            else
                body.Add(line);
        }

        void Add(LayoutRegionKind k, List<LayoutLine> list, string? skip = null)
        {
            if (list.Count == 0)
                return;
            var ymin = list.Min(l => l.MinY);
            var ymax = list.Max(l => l.MaxY);
            regions.Add(new LayoutRegion
            {
                Kind = k,
                YMin = ymin,
                YMax = ymax,
                Lines = list.ToList(),
                SkipReason = skip
            });
        }

        Add(LayoutRegionKind.Noise, noise, "email_phone_or_metadata_pattern");
        Add(LayoutRegionKind.Header, header);
        Add(LayoutRegionKind.Body, body);
        Add(LayoutRegionKind.Footer, footer);

        return regions;
    }

    private static bool LooksLikeNoiseLine(string plain)
    {
        if (plain.Length > 220)
            return true;
        if (EmailRx.IsMatch(plain))
            return true;
        if (PhoneRx.IsMatch(plain))
            return true;
        return false;
    }
}
