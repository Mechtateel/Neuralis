using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Contracts.Auth;
using NeuralisAPI.Models;

namespace NeuralisAPI.Data;

public static class DemoAccountsSeeder
{
    private const string DemoPassword = "123456";

    public static async Task SeedAsync(AppDbContext db)
    {
        await SeedDemoDoctorAsync(
            db,
            email: "therapist@test.com",
            fullName: "Dr. Demo Therapist",
            specialization: DoctorSpecializations.Therapist);

        await SeedDemoDoctorAsync(
            db,
            email: "ent@test.com",
            fullName: "Dr. Demo ENT",
            specialization: DoctorSpecializations.Ent);

        await SeedDemoDoctorAsync(
            db,
            email: "derm@test.com",
            fullName: "Dr. Demo Dermatologist",
            specialization: DoctorSpecializations.Dermatologist);

        await SeedDemoDoctorAsync(
            db,
            email: "lab@test.com",
            fullName: "Demo Lab Technician",
            specialization: DoctorSpecializations.LabTechnician);

        await SeedDemoDoctorAsync(
            db,
            email: "urologist@test.com",
            fullName: "Dr. John Uro",
            specialization: DoctorSpecializations.Urologist);

        await SeedDemoDoctorAsync(
            db,
            email: "proctologist@test.com",
            fullName: "Dr. Alex Procto",
            specialization: DoctorSpecializations.Proctologist);
    }

    private static async Task SeedDemoDoctorAsync(
        AppDbContext db,
        string email,
        string fullName,
        string specialization)
    {
        var user = await db.Users
            .Include(u => u.DoctorProfile)
            .FirstOrDefaultAsync(u => u.Email == email);

        var hash = BCrypt.Net.BCrypt.HashPassword(DemoPassword);

        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                PasswordHash = hash,
                Role = UserRole.Doctor,
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var profile = new DoctorProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Specialization = specialization,
                LicenseNumber = "DEMO-LICENSE",
                IsVerified = true,
                Status = DoctorApplicationStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                ReviewedAt = DateTime.UtcNow
            };
            db.DoctorProfiles.Add(profile);
            await db.SaveChangesAsync();
            return;
        }

        user.PasswordHash = hash;
        user.FullName = fullName;
        user.Role = UserRole.Doctor;

        if (user.DoctorProfile == null)
        {
            db.DoctorProfiles.Add(new DoctorProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Specialization = specialization,
                LicenseNumber = "DEMO-LICENSE",
                IsVerified = true,
                Status = DoctorApplicationStatus.Approved,
                CreatedAt = DateTime.UtcNow,
                ReviewedAt = DateTime.UtcNow
            });
        }
        else
        {
            user.DoctorProfile.Specialization = specialization;
            user.DoctorProfile.IsVerified = true;
            user.DoctorProfile.Status = DoctorApplicationStatus.Approved;
            user.DoctorProfile.LicenseNumber = "DEMO-LICENSE";
            user.DoctorProfile.ReviewedAt ??= DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }
}
