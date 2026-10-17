using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Data;
using NeuralisAPI.Models;
using NeuralisAPI.Contracts.Auth;
using NeuralisAPI.Contracts.Doctors;
using System.Security.Claims;

namespace NeuralisAPI.Controllers
{
    [ApiController]
    [Route("api/doctor/apply")]
    [Authorize] // любой залогиненный пользователь
    public class DoctorApplicationController : ControllerBase
    {
        private readonly AppDbContext _db;

        public DoctorApplicationController(AppDbContext db)
        {
            _db = db;
        }

        [HttpPost]
        public async Task<IActionResult> Apply(CreateDoctorProfileRequest request)
        {
            var userId = User.FindFirstValue("id");
            if (userId == null)
                return Unauthorized();

            var guid = Guid.Parse(userId);

            // если профиль уже существует — не даём повторно
            var exists = await _db.DoctorProfiles
                .AnyAsync(d => d.UserId == guid);

            if (exists)
                return BadRequest("Doctor profile already exists");

            var profile = new DoctorProfile
            {
                Id = Guid.NewGuid(),
                UserId = guid,
                Specialization = request.Specialization,
                LicenseNumber = request.LicenseNumber,
                IsVerified = false
            };

            _db.DoctorProfiles.Add(profile);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Doctor application submitted",
                doctorId = profile.Id
            });
        }
    }
}
