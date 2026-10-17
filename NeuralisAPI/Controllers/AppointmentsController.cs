using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Contracts.Appointments;
using NeuralisAPI.Data;
using NeuralisAPI.Models;
using System.Security.Claims;

namespace NeuralisAPI.Controllers
{
    [ApiController]
    [Route("api/appointments")]
    [Authorize]
    public class AppointmentsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AppointmentsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request)
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null)
                return Unauthorized();

            if (request.DoctorProfileId == Guid.Empty)
                return BadRequest(new { message = "Doctor must be selected" });

            var patientId = Guid.Parse(userIdClaim.Value);

            var doctor = await _db.DoctorProfiles
                .FirstOrDefaultAsync(d => d.Id == request.DoctorProfileId);

            if (doctor == null)
                return NotFound("Doctor not found");

            if (!doctor.IsVerified || doctor.Status != DoctorApplicationStatus.Approved)
                return BadRequest("Doctor is not available for booking");

            if (DoctorSpecializations.IsLabTechnician(doctor.Specialization))
                return BadRequest("This provider does not accept appointments.");

            if (request.ScheduledAtUtc <= DateTime.UtcNow)
                return BadRequest("Scheduled time must be in the future");

            var slotTaken = await _db.Appointments.AnyAsync(a =>
                a.DoctorProfileId == request.DoctorProfileId
                && a.ScheduledAtUtc == request.ScheduledAtUtc
                && a.Status != AppointmentStatus.Cancelled);

            if (slotTaken)
                return Conflict("This time slot is already booked");

            var appointment = new Appointment
            {
                Id = Guid.NewGuid(),
                PatientUserId = patientId,
                DoctorProfileId = doctor.Id,
                ScheduledAtUtc = request.ScheduledAtUtc,
                Status = AppointmentStatus.Confirmed
            };

            _db.Appointments.Add(appointment);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                appointmentId = appointment.Id,
                patientUserId = appointment.PatientUserId,
                doctorProfileId = appointment.DoctorProfileId,
                scheduledAtUtc = appointment.ScheduledAtUtc,
                status = appointment.Status.ToString()
            });
        }

        [HttpPost("{id:guid}/cancel")]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null)
                return Unauthorized();

            var patientId = Guid.Parse(userIdClaim.Value);

            var appointment = await _db.Appointments
                .FirstOrDefaultAsync(a => a.Id == id && a.PatientUserId == patientId);

            if (appointment == null)
                return NotFound();

            if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed)
                return BadRequest("This appointment cannot be cancelled.");

            appointment.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync();

            return Ok(new { status = appointment.Status.ToString() });
        }

        [HttpGet("my")]
        public async Task<IActionResult> MyAppointments()
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null)
                return Unauthorized();

            var patientId = Guid.Parse(userIdClaim.Value);

            var items = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.PatientUserId == patientId)
                .OrderBy(a => a.ScheduledAtUtc)
                .Select(a => new
                {
                    appointmentId = a.Id,
                    scheduledAtUtc = a.ScheduledAtUtc,
                    status = a.Status.ToString(),
                    doctorProfileId = a.DoctorProfileId,
                    doctorSpecialization = a.DoctorProfile.Specialization,
                    doctorEmail = a.DoctorProfile.User.Email,
                    doctorFullName = a.DoctorProfile.User.FullName
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("doctor")]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> DoctorAppointments()
        {
            var userIdClaim = User.FindFirst("id");
            if (userIdClaim == null)
                return Unauthorized();

            var userId = Guid.Parse(userIdClaim.Value);

            var profile = await _db.DoctorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (profile == null)
                return NotFound("Doctor profile not found");

            if (DoctorSpecializations.IsLabTechnician(profile.Specialization))
                return Ok(Array.Empty<object>());

            var items = await _db.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorProfileId == profile.Id)
                .OrderBy(a => a.ScheduledAtUtc)
                .Select(a => new
                {
                    appointmentId = a.Id,
                    scheduledAtUtc = a.ScheduledAtUtc,
                    status = a.Status.ToString(),
                    patientUserId = a.PatientUserId,
                    patientEmail = a.Patient.Email,
                    patientFullName = a.Patient.FullName
                })
                .ToListAsync();

            return Ok(items);
        }
    }
}
