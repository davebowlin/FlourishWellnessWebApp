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
        public DbSet<SurveyEntity> SurveyEntities => Set<SurveyEntity>();
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

            modelBuilder.Entity<SurveyEntity>()
                .HasIndex(e => e.Year)
                .IsUnique();

            modelBuilder.Entity<SurveyEntity>()
                .HasMany(e => e.Sections)
                .WithOne(s => s.SurveyEntity)
                .HasForeignKey(s => s.SurveyEntityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Question>()
                .HasOne(q => q.SurveyEntity)
                .WithMany()
                .HasForeignKey(q => q.SurveyEntityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Response>()
                .HasOne(r => r.SurveyEntity)
                .WithMany()
                .HasForeignKey(r => r.SurveyEntityId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserSurveyStatus>()
                .HasIndex(x => new { x.UserId, x.SurveyEntityId })
                .IsUnique();
        }
    }
}