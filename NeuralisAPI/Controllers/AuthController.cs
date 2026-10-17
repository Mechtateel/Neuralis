using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Data;
using NeuralisAPI.Models;
using NeuralisAPI.Contracts;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using NeuralisAPI.Contracts.Auth;

namespace NeuralisAPI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly JwtService _jwtService;
        public AuthController(AppDbContext db, JwtService jwtService)
        {
            _db = db;
            _jwtService = jwtService;
        }

        // ---------- REGISTER ----------
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            // 1️⃣ Проверка email
            var exists = await _db.Users.AnyAsync(u => u.Email == request.Email);
            if (exists)
                return BadRequest("User with this email already exists");

            // 2️⃣ Хэшируем пароль
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // 3️⃣ Определяем роль (DEV)
            UserRole role = UserRole.User;
            if (!string.IsNullOrEmpty(request.Role))
            {
                Enum.TryParse<UserRole>(request.Role, out role);
            }

            // 4️⃣ Создаём пользователя
            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = request.FullName,
                Email = request.Email,
                PasswordHash = passwordHash,
                Role = role
            };

            // 5️⃣ Сохраняем
            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.FullName,
                user.Email,
                role = user.Role.ToString()
            });
        }




        // ---------- LOGIN ----------
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _db.Users
                .Include(u => u.RefreshTokens)
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
                return Unauthorized("Invalid email or password");

            var valid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            if (!valid)
                return Unauthorized("Invalid email or password");

            if (user.IsBlocked)
                return Unauthorized("User is blocked");



            // 🔐 access token
            var accessToken = _jwtService.GenerateAccessToken(user);

            // 🔁 новый refresh token
            var refreshToken = _jwtService.GenerateRefreshToken(user.Id);

            // ⛔ инвалидируем ВСЕ старые refresh токены
            foreach (var token in user.RefreshTokens)
                token.IsRevoked = true;

            _db.RefreshTokens.Add(refreshToken);
            await _db.SaveChangesAsync();

            // 🍪 КЛАДЁМ refresh token В COOKIE
            Response.Cookies.Append(
                "refreshToken",
                refreshToken.Token,
                GetRefreshCookieOptions()
            );

            // ❗ В JSON НЕ ВОЗВРАЩАЕМ refreshToken
            return Ok(new
            {
                accessToken,
                user.Id,
                user.FullName,
                user.Email
            });
        }


        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userIdClaim = User.FindFirst("id"); // ⬅ ВАЖНО

            if (userIdClaim == null)
                return Unauthorized("User id claim missing");

            var userId = Guid.Parse(userIdClaim.Value);

            var user = await _db.Users
                .Include(u => u.DoctorProfile)
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return Unauthorized();

            if (user.IsBlocked)
                return Unauthorized("User is blocked");

            var isVerified = user.DoctorProfile?.IsVerified ?? false;
            string? doctorSpecialization = user.Role == UserRole.Doctor
                ? user.DoctorProfile?.Specialization
                : null;

            return Ok(new
            {
                id = user.Id,
                userId = user.Id,
                fullName = user.FullName,
                email = user.Email,
                role = user.Role,
                isVerified,
                doctorSpecialization
            });
        }



        [Authorize(Roles = "Doctor")]
        [HttpGet("doctor-only")]
        public IActionResult DoctorEndpoint()
        {
            return Ok("Hello Doctor");
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin-only")]
        public IActionResult AdminEndpoint()
        {
            return Ok("Hello Admin");
        }



        // ---------- REFRESH ----------
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("refreshToken", out var refreshTokenValue))
                return Unauthorized("Refresh token missing");

            var storedToken = await _db.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshTokenValue);

            if (storedToken == null || storedToken.ExpiresAt < DateTime.UtcNow)
                return Unauthorized("Invalid refresh token");

            if (storedToken.User.IsBlocked)
                return Unauthorized("User is blocked");

            var accessToken = _jwtService.GenerateAccessToken(storedToken.User);

            return Ok(new { accessToken });
        }



        // ---------- LOGOUT ----------
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue("refreshToken", out var token))
            {
                var stored = await _db.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == token);

                if (stored != null)
                    stored.IsRevoked = true;

                await _db.SaveChangesAsync();
            }

            Response.Cookies.Delete("refreshToken");

            return Ok(new { message = "Logged out" });
        }


        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll()
        {
            var userId = User.FindFirst("id")?.Value;
            if (userId == null)
                return Unauthorized();

            var guid = Guid.Parse(userId);

            var tokens = await _db.RefreshTokens
                .Where(rt => rt.UserId == guid && !rt.IsRevoked)
                .ToListAsync();
    
            foreach (var token in tokens)
                token.IsRevoked = true;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Logged out from all devices"
            });
        }

        private CookieOptions GetRefreshCookieOptions()
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,           // true в проде
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(7)
            };
        }



    }
}