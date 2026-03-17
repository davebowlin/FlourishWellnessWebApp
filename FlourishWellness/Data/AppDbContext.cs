using FlourishWellness.Models;
using Microsoft.EntityFrameworkCore;

namespace FlourishWellness.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Section> Sections => Set<Section>();
        public DbSet<Question> Questions => Set<Question>();
        public DbSet<Response> Responses => Set<Response>();
        public DbSet<User> Users => Set<User>();
        public DbSet<SurveyYear> SurveyYears => Set<SurveyYear>();
        public DbSet<UserSurveyStatus> UserSurveyStatuses => Set<UserSurveyStatus>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Section>()
                .HasMany(s => s.Subsections)
                .WithOne(s => s.ParentSection)
                .HasForeignKey(s => s.ParentSectionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // PasswordHash is not present in the production database schema; ignore it so EF Core
            // does not attempt to SELECT or INSERT this column.
            modelBuilder.Entity<User>()
                .Ignore(u => u.PasswordHash);

            modelBuilder.Entity<SurveyYear>()
                .ToTable("SurveyYear");

            modelBuilder.Entity<SurveyYear>()
                .HasIndex(e => e.Year)
                .IsUnique();

            modelBuilder.Entity<SurveyYear>()
                .HasMany(e => e.Sections)
                .WithOne(s => s.SurveyYear)
                .HasForeignKey(s => s.SurveyYearId);

            modelBuilder.Entity<Section>()
                .Property(s => s.SurveyYearId).HasColumnName("SurveyYear");

            modelBuilder.Entity<Question>()
                .HasOne(q => q.SurveyYear)
                .WithMany()
                .HasForeignKey(q => q.SurveyYearId);

            modelBuilder.Entity<Question>()
                .Property(q => q.SurveyYearId).HasColumnName("SurveyYear");

            modelBuilder.Entity<Response>()
                .HasOne(r => r.SurveyYear)
                .WithMany()
                .HasForeignKey(r => r.SurveyYearId);

            modelBuilder.Entity<Response>()
                .Property(r => r.SurveyYearId).HasColumnName("SurveyYear");

            modelBuilder.Entity<UserSurveyStatus>()
                .Property(u => u.SurveyYearId).HasColumnName("SurveyEntityId");

            modelBuilder.Entity<UserSurveyStatus>()
                .HasIndex(x => new { x.UserId, x.SurveyYearId })
                .IsUnique();
        }
    }
}