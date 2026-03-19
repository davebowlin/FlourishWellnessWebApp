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
        public DbSet<Community> Community => Set<Community>();

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
                .Property(u => u.SurveyYearId).HasColumnName("SurveyYear");

            modelBuilder.Entity<UserSurveyStatus>()
                .HasIndex(x => new { x.UserId, x.SurveyYearId, x.CommunityKey })
                .IsUnique();

            modelBuilder.Entity<Community>()
                .ToTable("Community");

            modelBuilder.Entity<Community>()
                .HasKey(c => new { c.Id, c.CommunityKey });

            modelBuilder.Entity<Community>()
                .Property(c => c.Id)
                .HasColumnName("UserId")
                .ValueGeneratedNever();

            modelBuilder.Entity<Community>()
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.Id)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}