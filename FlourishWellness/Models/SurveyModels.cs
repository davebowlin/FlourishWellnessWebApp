namespace FlourishWellness.Models
{
    public class SurveyYear
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public SurveyYearStatus Status { get; set; } = SurveyYearStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<Section> Sections { get; set; } = new();
    }

    public enum SurveyYearStatus
    {
        Archived = 1,
        Active = 2
    }

    public class Section
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SurveyYearId { get; set; }
        public SurveyYear SurveyYear { get; set; } = null!;
        public int? ParentSectionId { get; set; }
        public Section? ParentSection { get; set; }
        public List<Section> Subsections { get; set; } = new();
        public List<Question> Questions { get; set; } = new();
    }

    public class Question
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int SurveyYearId { get; set; }
        public SurveyYear SurveyYear { get; set; } = null!;
        public int SectionId { get; set; }
        public Section Section { get; set; } = null!;
        public List<Response> Responses { get; set; } = new();
    }

    public class Response
    {
        public int Id { get; set; }
        public string Answer { get; set; } = string.Empty;
        public int SurveyYearId { get; set; }
        public SurveyYear SurveyYear { get; set; } = null!;
        public int QuestionId { get; set; }
        public Question Question { get; set; } = null!;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string SAMAccountName { get; set; } = string.Empty;
        public DateTime? CreateDate { get; set; }
        public DateTime? Modified { get; set; }
        public int? CommunityKey { get; set; }
    }

    public class ADFacilityUser
    {
        public string Facility { get; set; } = string.Empty;
        public string? CommunityKey { get; set; }
        public string SAMAccountName { get; set; } = string.Empty;
    }

    public class Community
    {
        public int Id { get; set; }
        public string SAMAccountName { get; set; } = string.Empty;
        public string Facility { get; set; } = string.Empty;
        public int CommunityKey { get; set; }
    }

    public class UserSurveyStatus
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int SurveyYearId { get; set; }
        public SurveyYear SurveyYear { get; set; } = null!;
        public bool IsCompleted { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string SAMAccountName { get; set; } = string.Empty;
        // The app no longer uses these AD attributes.
        // public string ExtensionAttribute10 { get; set; } = string.Empty;
        // public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum UserRole
    {
        Employee = 1,
        Manager = 2,
        Admin = 3
    }
}