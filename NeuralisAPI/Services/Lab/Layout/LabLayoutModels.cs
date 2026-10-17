using NeuralisAPI.Services.Lab;

namespace NeuralisAPI.Services.Lab.Layout;

/// <summary>Where word coordinates came from.</summary>
public enum LabLayoutCoordinateSource
{
    None,
    PdfPigGeometry,
    PseudoFromTextLines
}

/// <summary>One word (or merged token) with a bounding box in page space (PDF-style: Y increases upward).</summary>
public sealed class LayoutWord
{
    public required string Text { get; init; }
    public double X0 { get; init; }
    public double Y0 { get; init; }
    public double X1 { get; init; }
    public double Y1 { get; init; }

    public double CenterX => (X0 + X1) * 0.5;
    public double CenterY => (Y0 + Y1) * 0.5;
    public double Width => Math.Abs(X1 - X0);
    public double Height => Math.Abs(Y1 - Y0);
}

/// <summary>Words grouped into a reading-order line (similar baseline / Y).</summary>
public sealed class LayoutLine
{
    public required IReadOnlyList<LayoutWord> Words { get; init; }
    public double MinY { get; init; }
    public double MaxY { get; init; }
    public double MidY => (MinY + MaxY) * 0.5;
}

/// <summary>Semantic band on the page (header / body / footer).</summary>
public enum LayoutRegionKind
{
    Header,
    Body,
    Footer,
    Noise
}

public sealed class LayoutRegion
{
    public required LayoutRegionKind Kind { get; init; }
    public double YMin { get; init; }
    public double YMax { get; init; }
    public required IReadOnlyList<LayoutLine> Lines { get; init; }
    public string? SkipReason { get; init; }
}

/// <summary>Detected table: a set of geometric rows inside the body.</summary>
public sealed class TableRegion
{
    public required IReadOnlyList<TableGeometricRow> Rows { get; init; }
    public double GeometryConfidence { get; init; }
    public IReadOnlyList<double>? ColumnAnchors { get; init; }
}

/// <summary>One table row as word clusters on one baseline band.</summary>
public sealed class TableGeometricRow
{
    public required IReadOnlyList<LayoutWord> Words { get; init; }
    public double RowY { get; init; }
}

/// <summary>Role buckets inferred from horizontal position (no analyte keywords).</summary>
public sealed class RowRoleAssignment
{
    public required string LabelText { get; init; }
    public required string ResultClusterText { get; init; }
    public string ReferenceClusterText { get; init; } = string.Empty;
    public string FarRightText { get; init; } = string.Empty;
    public double Confidence { get; init; }
}

public sealed class LabPageLayout
{
    public int PageNumber { get; init; }
    public double PageWidth { get; init; }
    public double PageHeight { get; init; }
    public LabLayoutCoordinateSource Source { get; init; }
    public required IReadOnlyList<LayoutWord> Words { get; init; }
}

public sealed class LabLayoutReconstructedRow
{
    public int PageIndex { get; init; }
    public int RowIndexInTable { get; init; } = -1;
    public LayoutRegionKind Region { get; init; }
    public required string CleanedRowText { get; init; }
    public double GeometryConfidence { get; init; }
    public string ConfidenceLevel { get; init; } = "LOW";
    public string Column1 { get; init; } = string.Empty;
    public string Column2 { get; init; } = string.Empty;
    public string Column3 { get; init; } = string.Empty;
    public string ColumnRest { get; init; } = string.Empty;
    public string? RowBoundsDescription { get; init; }
    public string? SkippedReason { get; init; }
}

public sealed class LabLayoutExtractionDebug
{
    public required IReadOnlyList<string> Messages { get; init; }
    public required IReadOnlyList<LabLayoutReconstructedRow> RowTrace { get; init; }
}

public sealed class LabLayoutPipelineResult
{
    public required IReadOnlyList<LabValue> Values { get; init; }
    public required IReadOnlyList<LabLayoutReconstructedRow> ReconstructedRows { get; init; }
    public required LabLayoutExtractionDebug Debug { get; init; }
}
