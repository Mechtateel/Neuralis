using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Contracts.Ai;
using NeuralisAPI.Data;
using NeuralisAPI.Models;
using NeuralisAPI.Services;
using NeuralisAPI.Services.Lab;
using NeuralisAPI.Services.Lab.Layout;

namespace NeuralisAPI.Controllers;

/// <summary>Production lab results list, detail, and AI analysis (structured values when available).</summary>
[ApiController]
[Route("api/lab-results")]
[Authorize(Roles = "Doctor")]
public class LabResultsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<LabResultsController> _logger;
    private readonly LabAnalysisService _labAnalysis;
    private readonly AIService _aiService;

    public LabResultsController(
        AppDbContext db,
        ILogger<LabResultsController> logger,
        LabAnalysisService labAnalysis,
        AIService aiService)
    {
        _db = db;
        _logger = logger;
        _labAnalysis = labAnalysis;
        _aiService = aiService;
    }

    private sealed record AttachmentMeta(
        Guid Id,
        string FileName,
        Guid PatientUserId,
        Guid UploadedByDoctorProfileId,
        string ContentType,
        DateTime UploadedAtUtc,
        string Description,
        string Tags);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 150)
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userIdGuid))
            return Unauthorized();

        try
        {
            var profile = await _db.DoctorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == userIdGuid);

            if (profile == null)
                return NotFound("Doctor profile not found");

            if (!profile.IsVerified || profile.Status != DoctorApplicationStatus.Approved)
                return Forbid();

            take = Math.Clamp(take, 1, 500);

            IQueryable<PatientLabAttachment> query;

            if (DoctorSpecializations.IsLabTechnician(profile.Specialization))
            {
                query = _db.PatientLabAttachments
                    .AsNoTracking()
                    .Where(a => a.UploadedByDoctorProfileId == profile.Id);
            }
            else
            {
                var patientIds = await _db.Appointments
                    .AsNoTracking()
                    .Where(a => a.DoctorProfileId == profile.Id && a.Status != AppointmentStatus.Cancelled)
                    .Select(a => a.PatientUserId)
                    .Distinct()
                    .ToListAsync();

                if (patientIds.Count == 0)
                    return Ok(Array.Empty<object>());

                query = _db.PatientLabAttachments
                    .AsNoTracking()
                    .Where(a => patientIds.Contains(a.PatientUserId));
            }

            var list = await query
                .OrderByDescending(a => a.UploadedAtUtc)
                .Take(take)
                .Select(a => new
                {
                    id = a.Id,
                    patientUserId = a.PatientUserId,
                    patientFullName = a.Patient.FullName ?? "",
                    patientEmail = a.Patient.Email ?? "",
                    fileName = a.FileName,
                    uploadedAtUtc = a.UploadedAtUtc,
                    contentType = a.ContentType
                })
                .ToListAsync();

            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "lab-results list for user {UserId}", userIdGuid);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Strict JSON: analyses[].analysis_type + results[] (test_name, value, unit, reference_range, status).</summary>
    [HttpGet("{id:guid}/structured-json")]
    public async Task<IActionResult> GetStructuredJson(Guid id, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userIdGuid))
            return Unauthorized();

        try
        {
            var (_, err) = await TryGetAuthorizedMetaAsync(id, userIdGuid);
            if (err != null)
                return err;

            var fileRow = await _db.PatientLabAttachments
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => new { a.FileBytes, a.ContentType, a.FileName })
                .FirstOrDefaultAsync(cancellationToken);

            if (fileRow == null)
                return NotFound();

            var doc = _labAnalysis.BuildStructuredDocument(fileRow.FileBytes, fileRow.ContentType, fileRow.FileName);
            return Ok(doc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "lab-results structured-json {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/analysis")]
    public async Task<IActionResult> GetAnalysis(Guid id, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userIdGuid))
            return Unauthorized();

        try
        {
            var (_, err) = await TryGetAuthorizedMetaAsync(id, userIdGuid);
            if (err != null)
                return err;

            var fileRow = await _db.PatientLabAttachments
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => new { a.FileBytes, a.ContentType, a.FileName })
                .FirstOrDefaultAsync(cancellationToken);

            if (fileRow == null)
                return NotFound();

            var analysis = _labAnalysis.AnalyzeDocument(
                fileRow.FileBytes,
                fileRow.ContentType,
                fileRow.FileName,
                id);

            var narrative = analysis.Narrative;
            var labMarkers = analysis.Values
                .Zip(analysis.Rows, (v, r) => new LabMarkerInputDto
                {
                    Name = v.Name,
                    ValueDisplay = r.ValueDisplay,
                    Status = r.Status,
                })
                .ToList();

            _logger.LogInformation(
                "[LAB AI] OpenAI request sent via AIService (markers={MarkerCount}, priority={Priority})",
                labMarkers.Count,
                narrative.Priority);

            var aiLabInterpretation = await _aiService.AnalyzeLabAsync(
                new AnalyzeLabRequest
                {
                    Summary = narrative.Summary,
                    Priority = narrative.Priority,
                    Findings = narrative.Findings,
                    Recommendations = narrative.Recommendations,
                    LaboratoryValues = labMarkers,
                },
                cancellationToken);

            _logger.LogInformation(
                "[LAB AI] Controller response payload: ai_laboratory_interpretation_len={Len}, active={Active}",
                aiLabInterpretation.Length,
                !aiLabInterpretation.Trim().Equals("AI laboratory interpretation unavailable.", StringComparison.OrdinalIgnoreCase));

            return Ok(new
            {
                summary = narrative.Summary,
                priority = narrative.Priority,
                findings = narrative.Findings,
                recommendations = narrative.Recommendations,
                ai_laboratory_interpretation = aiLabInterpretation,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "lab-results analysis {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userIdGuid))
            return Unauthorized();

        try
        {
            var (meta, err) = await TryGetAuthorizedMetaAsync(id, userIdGuid);
            if (err != null)
                return err;

            var m = meta!;
            var testName = DeriveTestName(m.FileName);

            var fileRow = await _db.PatientLabAttachments
                .AsNoTracking()
                .Where(a => a.Id == id)
                .Select(a => new { a.FileBytes, a.ContentType })
                .FirstOrDefaultAsync(cancellationToken);

            object[] values = Array.Empty<object>();
            LabLayoutExtractionDebug? layoutDiag = null;
            if (fileRow != null)
            {
                var parse = _labAnalysis.Parse(fileRow.FileBytes, fileRow.ContentType, m.FileName);
                layoutDiag = parse.LayoutDiagnostics;
                var (docVals, docRows) = _labAnalysis.PrepareDoctorFacing(parse.Values);
                values = docVals
                    .Zip(docRows, (v, r) => new
                    {
                        pageIndex = v.PageIndex >= 0 ? v.PageIndex : v.SourceOrder / 1_000_000_000,
                        rowIndex = v.TableRowIndex >= 0 ? v.TableRowIndex : (v.SourceOrder % 1_000_000_000) / 1_000,
                        confidence = v.ParseConfidence,
                        resultKind = v.ValueType,
                        name = r.Name,
                        value = string.Equals(v.ValueType, "qualitative", StringComparison.OrdinalIgnoreCase)
                            ? (object?)(v.TextValue ?? string.Empty)
                            : (v.NumericValue ?? v.Value),
                        numericValue = string.Equals(v.ValueType, "qualitative", StringComparison.OrdinalIgnoreCase)
                            ? (double?)null
                            : (v.NumericValue ?? v.Value),
                        textValue = string.Equals(v.ValueType, "qualitative", StringComparison.OrdinalIgnoreCase)
                            ? v.TextValue
                            : null,
                        booleanValue = v.BooleanValue,
                        unit = string.Equals(v.ValueType, "qualitative", StringComparison.OrdinalIgnoreCase)
                            ? null
                            : v.Unit,
                        interpretation = r.Status.ToUpperInvariant(),
                        status = r.Status.ToUpperInvariant(),
                        refMin = v.HasReferenceRange ? v.RefMin : (double?)null,
                        refMax = v.HasReferenceRange ? v.RefMax : (double?)null,
                        section = v.Section,
                        lowConfidence = v.LowConfidence,
                    })
                    .Cast<object>()
                    .ToArray();
            }

            object? layoutDiagnostics = null;
            if (layoutDiag != null)
            {
                layoutDiagnostics = new
                {
                    messages = layoutDiag.Messages,
                    rowTrace = layoutDiag.RowTrace.Select(r => new
                    {
                        pageIndex = r.PageIndex,
                        rowIndex = r.RowIndexInTable,
                        region = r.Region.ToString(),
                        cleanedRowText = r.CleanedRowText,
                        geometryConfidence = r.GeometryConfidence,
                        confidenceLevel = r.ConfidenceLevel,
                        col1 = r.Column1,
                        col2 = r.Column2,
                        col3 = r.Column3,
                        colRest = r.ColumnRest,
                        rowBounds = r.RowBoundsDescription,
                        skippedReason = r.SkippedReason
                    }).ToList()
                };
            }

            return Ok(new
            {
                id = m.Id,
                testName,
                fileName = m.FileName,
                uploadedAtUtc = m.UploadedAtUtc,
                contentType = m.ContentType,
                description = m.Description,
                tags = m.Tags,
                values,
                layoutDiagnostics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "lab-results detail {Id}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    private async Task<(AttachmentMeta? Meta, IActionResult? Error)> TryGetAuthorizedMetaAsync(
        Guid attachmentId,
        Guid userIdGuid)
    {
        var profile = await _db.DoctorProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userIdGuid);

        if (profile == null)
            return (null, NotFound("Doctor profile not found"));

        if (!profile.IsVerified || profile.Status != DoctorApplicationStatus.Approved)
            return (null, Forbid());

        var meta = await _db.PatientLabAttachments
            .AsNoTracking()
            .Where(a => a.Id == attachmentId)
            .Select(a => new AttachmentMeta(
                a.Id,
                a.FileName,
                a.PatientUserId,
                a.UploadedByDoctorProfileId,
                a.ContentType,
                a.UploadedAtUtc,
                a.Description,
                a.Tags))
            .FirstOrDefaultAsync();

        if (meta == null)
            return (null, NotFound());

        if (DoctorSpecializations.IsLabTechnician(profile.Specialization))
        {
            if (meta.UploadedByDoctorProfileId != profile.Id)
                return (null, NotFound());
        }
        else
        {
            var canAccess = await _db.Appointments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.PatientUserId == meta.PatientUserId
                    && a.DoctorProfileId == profile.Id
                    && a.Status != AppointmentStatus.Cancelled);
            if (!canAccess)
                return (null, Forbid());
        }

        return (meta, null);
    }

    private static string DeriveTestName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "Laboratory document";
        var baseName = Path.GetFileNameWithoutExtension(fileName.Trim());
        return string.IsNullOrWhiteSpace(baseName) ? "Laboratory document" : baseName;
    }
}
