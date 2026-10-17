using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Contracts.Admin;
using NeuralisAPI.Data;
using NeuralisAPI.Models;
using System.Security.Claims;
using NeuralisAPI.Contracts.Common;
using NeuralisAPI.Contracts.Auth;


namespace NeuralisAPI.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AdminController(AppDbContext db)
        {
            _db = db;
        }

        // ✅ Подтверждение врача
        [HttpPost("verify-doctor/{doctorId}")]
        public async Task<IActionResult> VerifyDoctor(Guid doctorId)
        {
            var adminId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            var doctor = await _db.DoctorProfiles
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doctor == null)
                return NotFound("Doctor not found");

            if (doctor.Status == DoctorApplicationStatus.Approved)
                return BadRequest("Doctor already verified");

            if (doctor.Status == DoctorApplicationStatus.Rejected)
                return BadRequest("Rejected application cannot be approved");

            doctor.Status = DoctorApplicationStatus.Approved;
            doctor.IsVerified = true;
            doctor.ReviewedAt = DateTime.UtcNow;
            doctor.ReviewedByAdminId = adminId;

            doctor.User.Role = UserRole.Doctor;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Doctor verified",
                doctorId = doctor.Id,
                userId = doctor.UserId,
                email = doctor.User.Email
            });
        }


        [Authorize(Roles = "Admin")]
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _db.Users
                .Include(u => u.DoctorProfile)
                .OrderByDescending(u => u.CreatedAt)
                .Select(u => new AdminUserResponse
                {
                    Id = u.Id,
                    Email = u.Email,
                    Role = u.Role.ToString(),
                    IsBlocked = u.IsBlocked,
                    CreatedAt = u.CreatedAt,

                    // ✅ ИСТОЧНИК ИМЕНИ
                    FullName = u.FullName,

                    HasDoctorProfile = u.DoctorProfile != null,
                    IsDoctorVerified = u.DoctorProfile != null
                        ? u.DoctorProfile.IsVerified
                        : null
                })
                .ToListAsync();

            return Ok(users);
        }





    [Authorize(Roles = "Admin")]
    [HttpPost("users/{userId}/block")]
    public async Task<IActionResult> BlockUser(Guid userId)
    {
        var adminId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (adminId == userId)
            return BadRequest("Admin cannot block himself");

        var user = await _db.Users.FindAsync(userId);
        if (user == null)
            return NotFound("User not found");

        if (user.IsBlocked)
            return BadRequest("User is already blocked");

        user.IsBlocked = true;
        await _db.SaveChangesAsync();

        return Ok(new { message = "User blocked" });
    }



    [Authorize(Roles = "Admin")]
        [HttpPost("users/{userId}/unblock")]
        public async Task<IActionResult> UnblockUser(Guid userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
                return NotFound("User not found");

            if (!user.IsBlocked)
                return BadRequest("User is not blocked");

            user.IsBlocked = false;
            await _db.SaveChangesAsync();

            return Ok(new { message = "User unblocked" });
        }

        [HttpGet("doctor-applications")]
        public async Task<IActionResult> GetDoctorApplications(
            [FromQuery] DoctorApplicationStatus status = DoctorApplicationStatus.Pending,
            [FromQuery] string? search = null,
            [FromQuery] string? specialization = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 6)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 20) pageSize = 6;

            var query = _db.DoctorProfiles
                .Include(d => d.User)
                .Where(d => d.Status == status)
                .AsQueryable();

            // 🔍 Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(d =>
                    d.User.FullName.ToLower().Contains(s) ||
                    d.User.Email.ToLower().Contains(s) ||
                    d.Specialization.ToLower().Contains(s)
                );
            }

            // 🧠 Filter by specialization
            if (!string.IsNullOrWhiteSpace(specialization))
            {
                query = query.Where(d =>
                    d.Specialization == specialization
                );
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new DoctorApplicationResponse
                {
                    DoctorId = d.Id,
                    UserId = d.UserId,
                    Email = d.User.Email,
                    FullName = d.User.FullName,
                    Specialization = d.Specialization,
                    LicenseNumber = d.LicenseNumber,
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync();

            var response = new PagedResponse<DoctorApplicationResponse>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };

            return Ok(response);
        }



        [HttpPost("reject-doctor/{doctorId}")]
        public async Task<IActionResult> RejectDoctor(
            Guid doctorId,
            [FromBody] RejectDoctorRequest request)
        {
            var adminId = Guid.Parse(
                User.FindFirstValue(ClaimTypes.NameIdentifier)!
            );

            var doctor = await _db.DoctorProfiles
                .FirstOrDefaultAsync(d => d.Id == doctorId);

            if (doctor == null)
                return NotFound("Doctor application not found");

            if (doctor.Status == DoctorApplicationStatus.Approved)
                return BadRequest("Approved doctor cannot be rejected");

            if (doctor.Status == DoctorApplicationStatus.Rejected)
                return BadRequest("Doctor application already rejected");

            doctor.Status = DoctorApplicationStatus.Rejected;
            doctor.IsVerified = false;
            doctor.RejectionReason = request.Reason;
            doctor.ReviewedAt = DateTime.UtcNow;
            doctor.ReviewedByAdminId = adminId;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Doctor application rejected",
                doctorId = doctor.Id
            });
        }
    }
}
