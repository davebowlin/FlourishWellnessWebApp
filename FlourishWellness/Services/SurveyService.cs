using FlourishWellness.Data;
using FlourishWellness.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FlourishWellness.Services
{
    public class SurveyService
    {
        private readonly IDbContextFactory<AppDbContext> _factory;

        public SurveyService(IDbContextFactory<AppDbContext> factory)
        {
            _factory = factory;
        }

        public async Task<List<SurveyEntity>> GetSurveyEntitiesAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.SurveyEntities
                .OrderByDescending(e => e.Year)
                .ToListAsync();
        }

        public async Task<SurveyEntity?> GetActiveSurveyEntityAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.SurveyEntities
                .OrderByDescending(e => e.Year)
                .FirstOrDefaultAsync(e => e.Status == SurveyEntityStatus.Active);
        }

        public async Task<SurveyEntity> ArchiveActiveAndCreateNextAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            activeEntity.Status = SurveyEntityStatus.Archived;

            var nextYear = activeEntity.Year + 1;
            while (await context.SurveyEntities.AnyAsync(e => e.Year == nextYear))
            {
                nextYear++;
            }

            var nextEntity = new SurveyEntity
            {
                Year = nextYear,
                Status = SurveyEntityStatus.Active,
                CreatedAt = DateTime.UtcNow
            };
            context.SurveyEntities.Add(nextEntity);
            await context.SaveChangesAsync();

            await CloneSectionsAndQuestionsAsync(context, activeEntity.Id, nextEntity.Id);
            await context.SaveChangesAsync();

            return nextEntity;
        }

        public async Task SetActiveSurveyEntityAsync(int surveyEntityId)
        {
            using var context = await _factory.CreateDbContextAsync();

            var target = await context.SurveyEntities.FindAsync(surveyEntityId);
            if (target == null)
            {
                throw new InvalidOperationException("Survey entity not found.");
            }

            var currentActive = await context.SurveyEntities
                .FirstOrDefaultAsync(e => e.Status == SurveyEntityStatus.Active);

            if (currentActive != null && currentActive.Id != target.Id)
            {
                currentActive.Status = SurveyEntityStatus.Archived;
            }

            target.Status = SurveyEntityStatus.Active;
            await context.SaveChangesAsync();
        }

        public async Task<List<Section>> GetSectionsAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            return await context.Sections
                .Where(s => s.ParentSectionId == null && s.SurveyEntityId == activeEntity.Id)
                .Include(s => s.Questions)
                .Include(s => s.Subsections)
                .ThenInclude(sub => sub.Questions)
                .ToListAsync();
        }

        public async Task<List<Section>> GetSectionsWithResponsesAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            return await context.Sections
                .Where(s => s.ParentSectionId == null && s.SurveyEntityId == activeEntity.Id)
                .Include(s => s.Questions)
                    .ThenInclude(q => q.Responses)
                        .ThenInclude(r => r.User)
                .Include(s => s.Subsections)
                    .ThenInclude(sub => sub.Questions)
                        .ThenInclude(q => q.Responses)
                            .ThenInclude(r => r.User)
                .ToListAsync();
        }

        public async Task AddSectionAsync(Section sec)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            sec.SurveyEntityId = activeEntity.Id;
            context.Sections.Add(sec);
            await context.SaveChangesAsync();
        }

        public async Task AddQuestionAsync(Question question)
        {
            using var context = await _factory.CreateDbContextAsync();

            var section = await context.Sections.FirstOrDefaultAsync(s => s.Id == question.SectionId);
            if (section == null)
            {
                throw new InvalidOperationException("Section not found for new question.");
            }

            question.SurveyEntityId = section.SurveyEntityId;
            context.Questions.Add(question);
            await context.SaveChangesAsync();
        }

        public async Task AddResponseAsync(Response response)
        {
            using var context = await _factory.CreateDbContextAsync();
            context.Responses.Add(response);
            await context.SaveChangesAsync();
        }

        public async Task SaveUserResponsesAsync(int userId, Dictionary<int, string> responses)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            var existingResponses = await context.Responses
                .Where(r => r.UserId == userId && r.SurveyEntityId == activeEntity.Id)
                .ToListAsync();

            foreach (var kvp in responses)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value)) continue;

                var question = await context.Questions.FirstOrDefaultAsync(q => q.Id == kvp.Key && q.SurveyEntityId == activeEntity.Id);
                if (question == null)
                {
                    continue;
                }

                var existing = existingResponses.FirstOrDefault(r => r.QuestionId == kvp.Key);
                if (existing != null)
                {
                    existing.Answer = kvp.Value;
                }
                else
                {
                    context.Responses.Add(new Response
                    {
                        UserId = userId,
                        SurveyEntityId = activeEntity.Id,
                        QuestionId = kvp.Key,
                        Answer = kvp.Value
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        public async Task<Dictionary<int, string>> GetUserResponsesAsync(int userId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            return await context.Responses
                .Where(r => r.UserId == userId && r.SurveyEntityId == activeEntity.Id)
                .ToDictionaryAsync(r => r.QuestionId, r => r.Answer);
        }

        public async Task<bool> CompleteSurveyAsync(int userId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            var user = await context.Users.FindAsync(userId);
            if (user == null)
            {
                return false;
            }

            var totalQuestionCount = await context.Questions.CountAsync(q => q.SurveyEntityId == activeEntity.Id);
            var answeredCount = await context.Responses
                .Where(r => r.UserId == userId && r.SurveyEntityId == activeEntity.Id && !string.IsNullOrWhiteSpace(r.Answer))
                .Select(r => r.QuestionId)
                .Distinct()
                .CountAsync();

            if (totalQuestionCount == 0 || answeredCount < totalQuestionCount)
            {
                return false;
            }

            var status = await GetOrCreateUserSurveyStatusAsync(context, userId, activeEntity.Id);
            status.IsCompleted = true;
            status.UpdatedAt = DateTime.UtcNow;

            user.IsSurveyCompleted = true;
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsSurveyCompletedAsync(int userId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);
            var status = await context.UserSurveyStatuses
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SurveyEntityId == activeEntity.Id);

            return status?.IsCompleted ?? false;
        }

        public async Task UpdateSectionAsync(int sectionId, string newName)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);
            var sec = await context.Sections.FirstOrDefaultAsync(s => s.Id == sectionId && s.SurveyEntityId == activeEntity.Id);
            if (sec != null)
            {
                sec.Name = newName;
                await context.SaveChangesAsync();
            }
        }

        public async Task UpdateQuestionAsync(int questionId, string newText)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);
            var question = await context.Questions.FirstOrDefaultAsync(q => q.Id == questionId && q.SurveyEntityId == activeEntity.Id);
            if (question != null)
            {
                question.Text = newText;
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteSectionAsync(int sectionId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            var sec = await context.Sections
                .Include(s => s.Questions)
                    .ThenInclude(q => q.Responses)
                .Include(s => s.Subsections)
                .FirstOrDefaultAsync(s => s.Id == sectionId && s.SurveyEntityId == activeEntity.Id);

            if (sec != null)
            {
                context.Sections.Remove(sec);
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteQuestionAsync(int questionId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            var question = await context.Questions
                .Include(q => q.Responses)
                .FirstOrDefaultAsync(q => q.Id == questionId && q.SurveyEntityId == activeEntity.Id);

            if (question != null)
            {
                context.Questions.Remove(question);
                await context.SaveChangesAsync();
            }
        }

        public async Task<string> ClearAllResponsesWithBackupAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeEntity = await GetOrCreateActiveSurveyEntityAsync(context);

            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            var databaseFilePath = await GetMainDatabaseFilePathAsync(connection);
            if (string.IsNullOrWhiteSpace(databaseFilePath))
            {
                throw new InvalidOperationException("Could not resolve database file path for backup.");
            }

            if (!File.Exists(databaseFilePath))
            {
                throw new FileNotFoundException("Database file was not found for backup.", databaseFilePath);
            }

            var backupDirectory = Path.Combine(Path.GetDirectoryName(databaseFilePath)!, "backups");
            Directory.CreateDirectory(backupDirectory);

            var backupFileName = $"{Path.GetFileNameWithoutExtension(databaseFilePath)}-backup-{DateTime.Now:yyyyMMdd-HHmmss}.db";
            var backupFilePath = Path.Combine(backupDirectory, backupFileName);
            File.Copy(databaseFilePath, backupFilePath, overwrite: true);

            var allResponses = await context.Responses
                .Where(r => r.SurveyEntityId == activeEntity.Id)
                .ToListAsync();
            context.Responses.RemoveRange(allResponses);

            var statusRows = await context.UserSurveyStatuses
                .Where(s => s.SurveyEntityId == activeEntity.Id)
                .ToListAsync();

            foreach (var status in statusRows)
            {
                status.IsCompleted = false;
                status.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();

            return backupFilePath;
        }

        private static async Task<SurveyEntity> GetOrCreateActiveSurveyEntityAsync(AppDbContext context)
        {
            var activeEntity = await context.SurveyEntities
                .OrderByDescending(e => e.Year)
                .FirstOrDefaultAsync(e => e.Status == SurveyEntityStatus.Active);

            if (activeEntity != null)
            {
                return activeEntity;
            }

            var currentYear = DateTime.UtcNow.Year;
            var existingForYear = await context.SurveyEntities.FirstOrDefaultAsync(e => e.Year == currentYear);
            if (existingForYear != null)
            {
                existingForYear.Status = SurveyEntityStatus.Active;
                await context.SaveChangesAsync();
                return existingForYear;
            }

            activeEntity = new SurveyEntity
            {
                Year = currentYear,
                Status = SurveyEntityStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            context.SurveyEntities.Add(activeEntity);
            await context.SaveChangesAsync();
            return activeEntity;
        }

        private static async Task<UserSurveyStatus> GetOrCreateUserSurveyStatusAsync(AppDbContext context, int userId, int surveyEntityId)
        {
            var status = await context.UserSurveyStatuses
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SurveyEntityId == surveyEntityId);

            if (status != null)
            {
                return status;
            }

            status = new UserSurveyStatus
            {
                UserId = userId,
                SurveyEntityId = surveyEntityId,
                IsCompleted = false,
                UpdatedAt = DateTime.UtcNow
            };

            context.UserSurveyStatuses.Add(status);
            await context.SaveChangesAsync();
            return status;
        }

        private static async Task CloneSectionsAndQuestionsAsync(AppDbContext context, int sourceSurveyEntityId, int targetSurveyEntityId)
        {
            var sourceSections = await context.Sections
                .Where(s => s.SurveyEntityId == sourceSurveyEntityId)
                .OrderBy(s => s.Id)
                .ToListAsync();

            var sourceQuestions = await context.Questions
                .Where(q => q.SurveyEntityId == sourceSurveyEntityId)
                .OrderBy(q => q.Id)
                .ToListAsync();

            var sectionIdMap = new Dictionary<int, int>();

            async Task CloneLevelAsync(int? sourceParentSectionId, int? newParentSectionId)
            {
                var sectionsAtLevel = sourceSections
                    .Where(s => s.ParentSectionId == sourceParentSectionId)
                    .OrderBy(s => s.Id)
                    .ToList();

                foreach (var sourceSection in sectionsAtLevel)
                {
                    var newSection = new Section
                    {
                        Name = sourceSection.Name,
                        ParentSectionId = newParentSectionId,
                        SurveyEntityId = targetSurveyEntityId
                    };

                    context.Sections.Add(newSection);
                    await context.SaveChangesAsync();
                    sectionIdMap[sourceSection.Id] = newSection.Id;

                    var sourceSectionQuestions = sourceQuestions
                        .Where(q => q.SectionId == sourceSection.Id)
                        .OrderBy(q => q.Id)
                        .ToList();

                    foreach (var sourceQuestion in sourceSectionQuestions)
                    {
                        context.Questions.Add(new Question
                        {
                            Text = sourceQuestion.Text,
                            SectionId = newSection.Id,
                            SurveyEntityId = targetSurveyEntityId
                        });
                    }

                    await context.SaveChangesAsync();
                    await CloneLevelAsync(sourceSection.Id, newSection.Id);
                }
            }

            await CloneLevelAsync(null, null);
        }

        private static async Task<string?> GetMainDatabaseFilePathAsync(System.Data.Common.DbConnection connection)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA database_list;";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var databaseName = reader[1]?.ToString();
                if (!string.Equals(databaseName, "main", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var filePath = reader[2]?.ToString();
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    return filePath;
                }
            }

            return null;
        }
    }
}