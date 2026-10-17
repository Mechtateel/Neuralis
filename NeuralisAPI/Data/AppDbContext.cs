using Microsoft.EntityFrameworkCore;
using NeuralisAPI.Contracts.Auth;
using NeuralisAPI.Models;

namespace NeuralisAPI.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<User> Users => Set<User>();

        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public DbSet<DoctorProfile> DoctorProfiles => Set<DoctorProfile>();

        public DbSet<News> News { get; set; }

        public DbSet<Appointment> Appointments => Set<Appointment>();

        public DbSet<PatientLabAttachment> PatientLabAttachments => Set<PatientLabAttachment>();

        public DbSet<Symptom> Symptoms => Set<Symptom>();

        public DbSet<KnowledgeRecommendation> Recommendations => Set<KnowledgeRecommendation>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Symptom>(entity =>
            {
                entity.HasIndex(s => s.Name).IsUnique();
            });

            modelBuilder.Entity<KnowledgeRecommendation>(entity =>
            {
                entity.ToTable("Recommendations");
            });

            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.HasOne(a => a.Patient)
                    .WithMany(u => u.AppointmentsAsPatient)
                    .HasForeignKey(a => a.PatientUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.DoctorProfile)
                    .WithMany(d => d.Appointments)
                    .HasForeignKey(a => a.DoctorProfileId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PatientLabAttachment>(entity =>
            {
                entity.HasOne(x => x.Patient)
                    .WithMany()
                    .HasForeignKey(x => x.PatientUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.UploadedBy)
                    .WithMany()
                    .HasForeignKey(x => x.UploadedByDoctorProfileId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
