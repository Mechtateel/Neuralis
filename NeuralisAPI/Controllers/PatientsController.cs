using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Contracts.Auth;
using NeuralisAPI.Data;

namespace NeuralisAPI.Controllers;

[ApiController]
[Authorize(Roles = "Doctor,Admin")]
[Route("")]
public class PatientsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PatientsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /patients
    [HttpGet("patients")]
    // Backward compatibility for existing clients that call /api/patients
    [HttpGet("api/patients")]
    public async Task<IActionResult> GetPatients()
    {
        var utcNow = DateTime.UtcNow;
        var recentThreshold = utcNow.AddDays(-14);

        var rows = await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.User)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.CreatedAt,
                u.IsBlocked,
                AppointmentsCount = u.AppointmentsAsPatient.Count(),
                LastAppointmentUtc = u.AppointmentsAsPatient
                    .OrderByDescending(a => a.ScheduledAtUtc)
                    .Select(a => (DateTime?)a.ScheduledAtUtc)
                    .FirstOrDefault()
            })
            .OrderByDescending(u => u.LastAppointmentUtc ?? u.CreatedAt)
            .ToListAsync();

        var patients = rows.Select(u =>
        {
            var status = u.IsBlocked
                ? "DISCHARGED"
                : u.CreatedAt >= recentThreshold
                    ? "NEW"
                    : u.AppointmentsCount > 0
                        ? "RETURNING"
                        : "ACTIVE";

            var idN = u.Id.ToString("N").ToUpperInvariant();
            var patientCode = $"PX-{idN[..6]}";

            return new
            {
                id = u.Id,
                fullName = u.FullName,
                email = u.Email,
                patientCode,
                status,
                lastSync = u.LastAppointmentUtc ?? u.CreatedAt,
                createdAt = u.CreatedAt
            };
        }).ToList();

        return Ok(patients);
    }
}
