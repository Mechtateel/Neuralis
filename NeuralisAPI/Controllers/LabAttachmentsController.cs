using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Data;
using NeuralisAPI.Models;

namespace NeuralisAPI.Controllers;

[ApiController]
[Route("api/lab")]
[Authorize(Roles = "Doctor")]
public class LabAttachmentsController : ControllerBase
{
    private const long MaxFileBytes = 12 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly ILogger<LabAttachmentsController> _logger;

    public LabAttachmentsController(AppDbContext db, ILogger<LabAttachmentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// EF Core cannot translate <see cref="DoctorSpecializations.IsLabTechnician"/> into SQL.
    /// Use a separate lookup of clinical (non-lab) doctor profile IDs, then filter appointments.
    /// </summary>
    private async Task<List<Guid>> GetClinicalDoctorProfileIdsAsync(CancellationToken ct = default)
    {
        var labSpecLower = DoctorSpecializations.LabTechnician.ToLowerInvariant();

        return await _db.DoctorProfiles
            .AsNoTracking()
            .Where(d => d.Specialization.ToLower() != labSpecLower)
            .Select(d => d.Id)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Patients who have (or had) appointments with clinical doctors — lab may attach results for these cases only.
    /// </summary>
    [HttpGet("assigned-patients")]
    public async Task<IActionResult> GetAssignedPatients()
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdStr))
            return Unauthorized();

        if (!Guid.TryParse(userIdStr, out var userIdGuid))
        {
            _logger.LogWarning("assigned-patients: invalid id claim {Claim}", userIdStr);
            return Unauthorized();
        }

        var role = User.FindFirst(ClaimTypes.Role)?.Value
                   ?? User.FindFirst("role")?.Value
                   ?? "(missing)";

        try
        {
            var profile = await _db.DoctorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == userIdGuid);

            if (profile == null)
            {
                _logger.LogWarning("assigned-patients: no doctor profile for user {UserId}", userIdGuid);
                return NotFound("Doctor profile not found");
            }

            _logger.LogInformation(
                "assigned-patients: user {UserId}, role {Role}, specialization {Specialization}",
                userIdGuid,
                role,
                profile.Specialization ?? "(null)");

            if (!profile.IsVerified || profile.Status != DoctorApplicationStatus.Approved)
                return Forbid();

            List<Guid> patientIds;
            if (DoctorSpecializations.IsLabTechnician(profile.Specialization))
            {
                var clinicalDoctorIds = await GetClinicalDoctorProfileIdsAsync();

                if (clinicalDoctorIds.Count == 0)
                {
                    _logger.LogInformation("assigned-patients: no clinical doctor profiles; returning empty list");
                    return Ok(new List<object>());
                }

                patientIds = await _db.Appointments
                    .AsNoTracking()
                    .Where(a => a.Status != AppointmentStatus.Cancelled)
                    .Where(a => clinicalDoctorIds.Contains(a.DoctorProfileId))
                    .Select(a => a.PatientUserId)
                    .Distinct()
                    .ToListAsync();
            }
            else
            {
                // Clinical doctor: patients with active appointments on this profile.
                patientIds = await _db.Appointments
                    .AsNoTracking()
                    .Where(a => a.DoctorProfileId == profile.Id && a.Status != AppointmentStatus.Cancelled)
                    .Select(a => a.PatientUserId)
                    .Distinct()
                    .ToListAsync();
            }

            if (patientIds.Count == 0)
                return Ok(new List<object>());

            var patientsWithUploads = await _db.PatientLabAttachments
                .AsNoTracking()
                .Where(a => patientIds.Contains(a.PatientUserId))
                .Select(a => a.PatientUserId)
                .Distinct()
                .ToListAsync();

            var uploadSet = patientsWithUploads.ToHashSet();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => patientIds.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var items = users.Select(u => new
            {
                id = u.Id,
                patientUserId = u.Id,
                name = u.FullName ?? "Unknown",
                fullName = u.FullName ?? "Unknown",
                email = u.Email ?? "",
                hasUploadedResults = uploadSet.Contains(u.Id)
            }).ToList();

            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in assigned-patients endpoint for user {UserId}", userIdGuid);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    private async Task<IActionResult> ListRecentLabFilesAsync(Guid userIdGuid, int take, string logContext)
    {
        try
        {
            var profile = await _db.DoctorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == userIdGuid);

            if (profile == null)
                return NotFound("Doctor profile not found");

            if (!DoctorSpecializations.IsLabTechnician(profile.Specialization))
                return Forbid();

            if (!profile.IsVerified || profile.Status != DoctorApplicationStatus.Approved)
                return Forbid();

            take = Math.Clamp(take, 1, 50);

            var list = await _db.PatientLabAttachments
                .AsNoTracking()
                .Where(a => a.UploadedByDoctorProfileId == profile.Id)
                .OrderByDescending(a => a.UploadedAtUtc)
                .Take(take)
                .Select(a => new
                {
                    attachmentId = a.Id,
                    fileName = a.FileName,
                    patientUserId = a.PatientUserId,
                    patientName = a.Patient.FullName ?? "Unknown",
                    uploadedAtUtc = a.UploadedAtUtc,
                    sizeBytes = a.FileBytes.Length,
                    tags = a.Tags,
                    uploadedByName = a.UploadedBy.User.FullName ?? a.UploadedBy.User.Email ?? "Lab"
                })
                .ToListAsync();

            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in {Context} for user {UserId}", logContext, userIdGuid);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Recent files uploaded by the current lab technician.</summary>
    [HttpGet("recent-uploads")]
    public async Task<IActionResult> GetRecentUploads([FromQuery] int take = 15)
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userIdGuid))
            return Unauthorized();

        return await ListRecentLabFilesAsync(userIdGuid, take, "recent-uploads");
    }

    /// <summary>Same payload as recent-uploads; default page size for dashboard widgets.</summary>
    [HttpGet("files")]
    public async Task<IActionResult> GetLabFiles([FromQuery] int take = 10)
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userIdGuid))
            return Unauthorized();

        return await ListRecentLabFilesAsync(userIdGuid, take, "files");
    }

    /// <summary>Upload history for one patient (current lab technician only).</summary>
    [HttpGet("uploads")]
    public async Task<IActionResult> GetUploadsForPatient([FromQuery] Guid patientId, [FromQuery] int take = 50)
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

            if (patientId == Guid.Empty)
                return BadRequest(new { message = "patientId is required" });

            take = Math.Clamp(take, 1, 100);

            if (DoctorSpecializations.IsLabTechnician(profile.Specialization))
            {
                var clinicalDoctorIds = await GetClinicalDoctorProfileIdsAsync();
                var eligible = clinicalDoctorIds.Count > 0
                    && await _db.Appointments
                        .AsNoTracking()
                        .AnyAsync(a =>
                            a.PatientUserId == patientId
                            && a.Status != AppointmentStatus.Cancelled
                            && clinicalDoctorIds.Contains(a.DoctorProfileId));

                if (!eligible)
                    return BadRequest(new { message = "Patient is not in an assigned clinical case." });

                var labList = await _db.PatientLabAttachments
                    .AsNoTracking()
                    .Where(a => a.PatientUserId == patientId && a.UploadedByDoctorProfileId == profile.Id)
                    .OrderByDescending(a => a.UploadedAtUtc)
                    .Take(take)
                    .Select(a => new
                    {
                        attachmentId = a.Id,
                        fileName = a.FileName,
                        uploadedAtUtc = a.UploadedAtUtc,
                        sizeBytes = a.FileBytes.Length,
                        uploadedByName = a.UploadedBy.User.FullName ?? a.UploadedBy.User.Email ?? "Lab",
                        tags = a.Tags,
                        description = a.Description
                    })
                    .ToListAsync();

                return Ok(labList);
            }

            // Clinical doctor: all lab attachments for patients under their care.
            var doctorEligible = await _db.Appointments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.PatientUserId == patientId
                    && a.DoctorProfileId == profile.Id
                    && a.Status != AppointmentStatus.Cancelled);

            if (!doctorEligible)
                return BadRequest(new { message = "Patient is not assigned to you." });

            var doctorList = await _db.PatientLabAttachments
                .AsNoTracking()
                .Where(a => a.PatientUserId == patientId)
                .OrderByDescending(a => a.UploadedAtUtc)
                .Take(take)
                .Select(a => new
                {
                    attachmentId = a.Id,
                    fileName = a.FileName,
                    uploadedAtUtc = a.UploadedAtUtc,
                    sizeBytes = a.FileBytes.Length,
                    uploadedByName = a.UploadedBy.User.FullName ?? a.UploadedBy.User.Email ?? "Lab",
                    tags = a.Tags,
                    description = a.Description
                })
                .ToListAsync();

            return Ok(doctorList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "uploads list for patient {PatientId}", patientId);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Upload counts for the current lab technician (UTC day).</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetLabStats()
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

            if (!DoctorSpecializations.IsLabTechnician(profile.Specialization))
                return Forbid();

            if (!profile.IsVerified || profile.Status != DoctorApplicationStatus.Approved)
                return Forbid();

            var start = DateTime.UtcNow.Date;
            var end = start.AddDays(1);

            var processedToday = await _db.PatientLabAttachments.CountAsync(a =>
                a.UploadedByDoctorProfileId == profile.Id
                && a.UploadedAtUtc >= start
                && a.UploadedAtUtc < end);

            return Ok(new { processedToday });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in lab stats for user {UserId}", userIdGuid);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Download lab file bytes (same authorization as attachments/.../download).</summary>
    [HttpGet("attachments/{id:guid}/download")]
    [HttpGet("file/{id:guid}")]
    public async Task<IActionResult> DownloadAttachment(Guid id)
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

            PatientLabAttachment? att = await _db.PatientLabAttachments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (att == null)
                return NotFound();

            if (DoctorSpecializations.IsLabTechnician(profile.Specialization))
            {
                if (att.UploadedByDoctorProfileId != profile.Id)
                    return NotFound();
            }
            else
            {
                var canAccess = await _db.Appointments
                    .AsNoTracking()
                    .AnyAsync(a =>
                        a.PatientUserId == att.PatientUserId
                        && a.DoctorProfileId == profile.Id
                        && a.Status != AppointmentStatus.Cancelled);
                if (!canAccess)
                    return Forbid();
            }

            return File(att.FileBytes, att.ContentType, att.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading attachment {AttachmentId}", id);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileBytes + 64 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] Guid patientUserId,
        [FromForm] string? description,
        [FromForm] string? tags,
        IFormFile file)
    {
        var userIdStr = User.FindFirst("id")?.Value;
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userIdGuid))
            return Unauthorized();

        var labProfile = await _db.DoctorProfiles
            .FirstOrDefaultAsync(d => d.UserId == userIdGuid);

        if (labProfile == null)
            return NotFound("Doctor profile not found");

        if (!DoctorSpecializations.IsLabTechnician(labProfile.Specialization))
            return BadRequest("Only lab technicians may upload lab results.");

        if (!labProfile.IsVerified || labProfile.Status != DoctorApplicationStatus.Approved)
            return Forbid();

        if (file == null || file.Length == 0)
            return BadRequest("File is required.");

        if (file.Length > MaxFileBytes)
            return BadRequest($"File must be under {MaxFileBytes / (1024 * 1024)} MB.");

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "image/png",
            "image/jpeg",
            "image/webp"
        };

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;

        if (!allowed.Contains(contentType))
            return BadRequest("Only PDF or image files are allowed.");

        var clinicalDoctorIds = await GetClinicalDoctorProfileIdsAsync();
        var eligible = clinicalDoctorIds.Count > 0
            && await _db.Appointments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.PatientUserId == patientUserId
                    && a.Status != AppointmentStatus.Cancelled
                    && clinicalDoctorIds.Contains(a.DoctorProfileId));

        if (!eligible)
            return BadRequest("Patient is not in an assigned clinical case.");

        try
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var tagsNormalized = NormalizeTagsCsv(tags);

            var entity = new PatientLabAttachment
            {
                Id = Guid.NewGuid(),
                PatientUserId = patientUserId,
                UploadedByDoctorProfileId = labProfile.Id,
                Description = (description ?? string.Empty).Trim(),
                Tags = tagsNormalized,
                FileName = string.IsNullOrWhiteSpace(file.FileName) ? "upload" : file.FileName.Trim(),
                ContentType = contentType,
                FileBytes = bytes,
                UploadedAtUtc = DateTime.UtcNow
            };

            _db.Set<PatientLabAttachment>().Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                attachmentId = entity.Id,
                patientUserId = entity.PatientUserId,
                fileName = entity.FileName,
                uploadedAtUtc = entity.UploadedAtUtc
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in lab upload for patient {PatientId}", patientUserId);
            return StatusCode(500, new { message = ex.Message });
        }
    }

    private static string NormalizeTagsCsv(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
            return string.Empty;

        var parts = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();

        return string.Join(',', parts);
    }
}
