using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Data;
using NeuralisAPI.Models;

namespace NeuralisAPI.Controllers
{
    /// <summary>
    /// Verified doctors available for patient booking (separate from <see cref="DoctorController"/> specialist routes).
    /// </summary>
    [ApiController]
    [Route("api/doctors")]
    [Authorize]
    public class PublicDoctorsController : ControllerBase
    {
        private const string LabSpec = "labtechnician";

        private readonly AppDbContext _db;

        public PublicDoctorsController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("available")]
        public async Task<IActionResult> GetAvailable([FromQuery] string? specialization = null)
        {
            Debug.WriteLine($"Filtering doctors by specialization={specialization ?? "(none)"}");

            // Do not call custom static helpers here — EF Core must translate the predicate to SQL.
            var query = _db.DoctorProfiles
                .AsNoTracking()
                .Include(d => d.User)
                .Where(d => d.IsVerified && d.Status == DoctorApplicationStatus.Approved)
                .Where(d => d.Specialization != null && d.Specialization.ToLower() != LabSpec);

            if (!string.IsNullOrWhiteSpace(specialization))
            {
                var spec = specialization.Trim();
                query = query.Where(d => d.Specialization == spec);
            }

            var list = await query
                .OrderBy(d => d.User.FullName)
                .Select(d => new
                {
                    doctorProfileId = d.Id,
                    fullName = d.User.FullName,
                    email = d.User.Email,
                    specialization = d.Specialization
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}
