using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Data;
using NeuralisAPI.Models;
using NeuralisAPI.Contracts.Doctors;
using System.Security.Claims;

namespace NeuralisAPI.Controllers
{
    [ApiController]
    [Route("api/doctor")]
    [Authorize(Roles = "Doctor")]
    public class DoctorController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DoctorController(AppDbContext db)
        {
            _db = db;
        }

        // ✅ GET /api/doctor/me
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue("id");
            if (userId == null)
                return Unauthorized();

            var guid = Guid.Parse(userId);

            var doctor = await _db.DoctorProfiles
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == guid);

            if (doctor == null)
                return NotFound("Doctor profile not found");

            return Ok(new
            {
                doctorId = doctor.Id,
                userId = doctor.UserId,
                email = doctor.User.Email,
                specialization = doctor.Specialization,
                isVerified = doctor.IsVerified
            });
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            var userId = User.FindFirstValue("id");
            if (userId == null)
                return Unauthorized();

            var guid = Guid.Parse(userId);

            var doctor = await _db.DoctorProfiles
                .FirstOrDefaultAsync(d => d.UserId == guid);

            if (doctor == null)
                return NotFound("Doctor profile not found");

            if (!doctor.IsVerified)
                return Forbid("Doctor is not verified");

            return Ok(new
            {
                doctorId = doctor.Id,
                isVerified = doctor.IsVerified,
                message = "Welcome doctor"
            });
        }





    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateDoctorProfileRequest request)
    {
        var userId = User.FindFirstValue("id");
        if (userId == null)
            return Unauthorized();

        var guid = Guid.Parse(userId);

        var doctor = await _db.DoctorProfiles
            .FirstOrDefaultAsync(d => d.UserId == guid);

        if (doctor == null)
            return NotFound("Doctor profile not found");

        // ❌ Запрещаем менять подтверждение
        if (doctor.IsVerified)
            return BadRequest("Verified doctor profile cannot be edited");

        doctor.Specialization = request.Specialization;
        doctor.LicenseNumber = request.LicenseNumber;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Doctor profile updated",
            doctorId = doctor.Id
        });
    }


    }
}
