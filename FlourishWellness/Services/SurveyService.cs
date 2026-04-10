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

        public async Task<List<SurveyYear>> GetSurveyYearsAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.SurveyYears
                .OrderByDescending(e => e.Year)
                .ToListAsync();
        }

        public async Task<SurveyYear?> GetActiveSurveyYearAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            return await context.SurveyYears
                .OrderByDescending(e => e.Year)
                .FirstOrDefaultAsync(e => e.Status == SurveyYearStatus.Active);
        }

        public async Task<SurveyYear> ArchiveActiveAndCreateNextAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            activeYear.Status = SurveyYearStatus.Archived;

            var nextYear = activeYear.Year + 1;
            while (await context.SurveyYears.AnyAsync(e => e.Year == nextYear))
            {
                nextYear++;
            }

            var nextYearEntity = new SurveyYear
            {
                Year = nextYear,
                Status = SurveyYearStatus.Active,
                CreatedAt = TimeHelper.CstNow
            };
            context.SurveyYears.Add(nextYearEntity);
            await context.SaveChangesAsync();

            await CloneSectionsAndQuestionsAsync(context, activeYear.Year, nextYearEntity.Year);
            await context.SaveChangesAsync();

            return nextYearEntity;
        }

        public async Task SetActiveSurveyYearAsync(int surveyYearId)
        {
            using var context = await _factory.CreateDbContextAsync();

            var target = await context.SurveyYears.FindAsync(surveyYearId);
            if (target == null)
            {
                throw new InvalidOperationException("Survey year not found.");
            }

            var currentActive = await context.SurveyYears
                .FirstOrDefaultAsync(e => e.Status == SurveyYearStatus.Active);

            if (currentActive != null && currentActive.Id != target.Id)
            {
                currentActive.Status = SurveyYearStatus.Archived;
            }

            target.Status = SurveyYearStatus.Active;
            await context.SaveChangesAsync();
        }

        public async Task<List<Section>> GetSectionsAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            return await context.Sections
                .Where(s => s.ParentSectionId == null && s.SurveyYearId == activeYear.Year)
                .Include(s => s.Questions)
                .Include(s => s.Subsections)
                .ThenInclude(sub => sub.Questions)
                .ToListAsync();
        }

        public async Task<List<Section>> GetSectionsWithResponsesAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            return await context.Sections
                .Where(s => s.ParentSectionId == null && s.SurveyYearId == activeYear.Year)
                .Include(s => s.Questions)
                    .ThenInclude(q => q.Responses)
                        .ThenInclude(r => r.User)
                .Include(s => s.Subsections)
                    .ThenInclude(sub => sub.Questions)
                        .ThenInclude(q => q.Responses)
                            .ThenInclude(r => r.User)
                .ToListAsync();
        }

        public async Task<List<Section>> GetSectionsWithCompletedResponsesAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            var sections = await context.Sections
                .Where(s => s.ParentSectionId == null && s.SurveyYearId == activeYear.Year)
                .Include(s => s.Questions)
                    .ThenInclude(q => q.Responses)
                        .ThenInclude(r => r.User)
                .Include(s => s.Subsections)
                    .ThenInclude(sub => sub.Questions)
                        .ThenInclude(q => q.Responses)
                            .ThenInclude(r => r.User)
                .ToListAsync();

            var completedStatusRows = await context.UserSurveyStatuses
                .Where(s => s.SurveyYearId == activeYear.Year && s.IsCompleted)
                .Select(s => new { s.UserId, s.CommunityKey })
                .ToListAsync();

            var completedKeys = completedStatusRows
                .Select(s => BuildCompletionKey(s.UserId, s.CommunityKey))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            FilterResponsesToCompletedStatus(sections, completedKeys);
            return sections;
        }

        public async Task AddSectionAsync(Section sec)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            sec.SurveyYearId = activeYear.Year;
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

            question.SurveyYearId = section.SurveyYearId;
            context.Questions.Add(question);
            await context.SaveChangesAsync();
        }

        public async Task AddResponseAsync(Response response)
        {
            using var context = await _factory.CreateDbContextAsync();
            context.Responses.Add(response);
            await context.SaveChangesAsync();
        }

        public async Task SaveUserResponsesAsync(int userId, Dictionary<int, string> responses, string samAccountName, string? communityKey)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);
            int? ck = int.TryParse(communityKey, out var parsedCk) ? parsedCk : 0;

            if (activeYear.Status != SurveyYearStatus.Active)
            {
                throw new InvalidOperationException("This survey year has been archived and cannot be modified.");
            }

            var status = await context.UserSurveyStatuses
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SurveyYearId == activeYear.Year && s.CommunityKey == ck);

            if (status?.IsCompleted == true)
            {
                throw new InvalidOperationException("This survey is submitted and locked. A manager or admin must unlock it before editing.");
            }

            var existingResponses = await context.Responses
                .Where(r => r.UserId == userId && r.SurveyYearId == activeYear.Year && r.CommunityKey == ck)
                .ToListAsync();

            foreach (var kvp in responses)
            {
                if (string.IsNullOrWhiteSpace(kvp.Value)) continue;

                var question = await context.Questions.FirstOrDefaultAsync(q => q.Id == kvp.Key && q.SurveyYearId == activeYear.Year);
                if (question == null)
                {
                    continue;
                }

                var existing = existingResponses.FirstOrDefault(r => r.QuestionId == kvp.Key);
                if (existing != null)
                {
                    if (existing.Answer != kvp.Value)
                    {
                        context.ResponseAuditLogs.Add(new ResponseAuditLog
                        {
                            ResponseId = existing.Id,
                            QuestionId = existing.QuestionId,
                            UserId = userId,
                            SAMAccountName = samAccountName,
                            OldAnswer = existing.Answer,
                            NewAnswer = kvp.Value,
                            ChangedAt = TimeHelper.CstNow
                        });
                    }
                    existing.Answer = kvp.Value;
                    existing.Modified = TimeHelper.CstNow;
                    existing.SAMAccountName = samAccountName;
                    existing.CommunityKey = ck;
                }
                else
                {
                    context.Responses.Add(new Response
                    {
                        UserId = userId,
                        SurveyYearId = activeYear.Year,
                        QuestionId = kvp.Key,
                        Answer = kvp.Value,
                        SAMAccountName = samAccountName,
                        CommunityKey = ck,
                        CreateDate = TimeHelper.CstNow
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        public async Task<Dictionary<int, string>> GetUserResponsesAsync(int userId, int? communityKey)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            return await context.Responses
                .Where(r => r.UserId == userId && r.SurveyYearId == activeYear.Year && r.CommunityKey == communityKey)
                .ToDictionaryAsync(r => r.QuestionId, r => r.Answer);
        }

        public async Task<bool> CompleteSurveyAsync(int userId, int? communityKey)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            if (activeYear.Status != SurveyYearStatus.Active)
            {
                return false;
            }

            var totalQuestionCount = await context.Questions.CountAsync(q => q.SurveyYearId == activeYear.Year);
            var answeredCount = await context.Responses
                .Where(r => r.UserId == userId && r.SurveyYearId == activeYear.Year && r.CommunityKey == communityKey && !string.IsNullOrWhiteSpace(r.Answer))
                .Select(r => r.QuestionId)
                .Distinct()
                .CountAsync();

            if (totalQuestionCount == 0 || answeredCount < totalQuestionCount)
            {
                return false;
            }

            var status = await GetOrCreateUserSurveyStatusAsync(context, userId, activeYear.Year, communityKey);
            status.IsCompleted = true;
            status.UpdatedAt = TimeHelper.CstNow;

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsSurveyCompletedAsync(int userId, int? communityKey)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);
            var status = await context.UserSurveyStatuses
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SurveyYearId == activeYear.Year && s.CommunityKey == communityKey);

            return status?.IsCompleted ?? false;
        }

        public async Task<DateTime?> GetSurveyCompletedAtAsync(int userId, int? communityKey)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);
            var status = await context.UserSurveyStatuses
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SurveyYearId == activeYear.Year && s.CommunityKey == communityKey);

            if (status?.IsCompleted != true)
            {
                return null;
            }

            return status.UpdatedAt;
        }

        public async Task<bool> IsCommunitySurveyLockedAsync(int? communityKey)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);
            return activeYear.Status != SurveyYearStatus.Active;
        }

        public async Task<List<SurveyLockRow>> GetSurveyLockRowsAsync(string filter = "locked")
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            var responseRows = await context.Responses
                .Where(r => r.SurveyYearId == activeYear.Year)
                .GroupBy(r => new { r.UserId, r.CommunityKey })
                .Select(g => new
                {
                    g.Key.UserId,
                    g.Key.CommunityKey,
                    LastResponseAt = g.Max(r => (DateTime?)(r.Modified ?? r.CreateDate))
                })
                .ToListAsync();

            var statusRows = await context.UserSurveyStatuses
                .Where(s => s.SurveyYearId == activeYear.Year)
                .ToListAsync();

            var totalQuestionCount = await context.Questions
                .Where(q => q.SurveyYearId == activeYear.Year)
                .CountAsync();

            var keySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var responseRow in responseRows)
            {
                keySet.Add(BuildCompletionKey(responseRow.UserId, responseRow.CommunityKey));
            }

            foreach (var statusRow in statusRows)
            {
                keySet.Add(BuildCompletionKey(statusRow.UserId, statusRow.CommunityKey));
            }

            if (keySet.Count == 0)
            {
                return new List<SurveyLockRow>();
            }

            var statusByKey = statusRows.ToDictionary(
                s => BuildCompletionKey(s.UserId, s.CommunityKey),
                s => s,
                StringComparer.OrdinalIgnoreCase);

            var responseTimeByKey = responseRows.ToDictionary(
                r => BuildCompletionKey(r.UserId, r.CommunityKey),
                r => r.LastResponseAt,
                StringComparer.OrdinalIgnoreCase);

            var userIds = keySet
                .Select(ParseCompletionKey)
                .Select(k => k.userId)
                .Distinct()
                .ToList();

            var communityKeys = keySet
                .Select(ParseCompletionKey)
                .Select(k => k.communityKey)
                .Where(k => k.HasValue)
                .Select(k => k!.Value)
                .Distinct()
                .ToList();

            var users = await context.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var communityRows = await context.Community
                .Where(c => userIds.Contains(c.Id) || communityKeys.Contains(c.CommunityKey))
                .ToListAsync();

            var distinctAnsweredCounts = await context.Responses
                .Where(r => r.SurveyYearId == activeYear.Year && !string.IsNullOrWhiteSpace(r.Answer))
                .GroupBy(r => new { r.UserId, r.CommunityKey })
                .Select(g => new
                {
                    g.Key.UserId,
                    g.Key.CommunityKey,
                    AnsweredCount = g.Select(r => r.QuestionId).Distinct().Count()
                })
                .ToListAsync();

            var answeredCountByKey = distinctAnsweredCounts.ToDictionary(
                r => BuildCompletionKey(r.UserId, r.CommunityKey),
                r => r.AnsweredCount,
                StringComparer.OrdinalIgnoreCase);

            var rows = new List<SurveyLockRow>();

            foreach (var key in keySet)
            {
                var (userId, communityKey) = ParseCompletionKey(key);
                var user = users.GetValueOrDefault(userId);
                var status = statusByKey.GetValueOrDefault(key);
                var facilityName = ResolveFacilityName(communityRows, userId, communityKey);

                var isLocked = status?.IsCompleted ?? false;
                var lastChangedAt = status?.UpdatedAt
                    ?? responseTimeByKey.GetValueOrDefault(key)
                    ?? TimeHelper.CstNow;

                if (string.Equals(filter, "locked", StringComparison.OrdinalIgnoreCase) && !isLocked)
                {
                    continue;
                }

                if (string.Equals(filter, "unlocked", StringComparison.OrdinalIgnoreCase) && isLocked)
                {
                    continue;
                }

                rows.Add(new SurveyLockRow
                {
                    UserId = userId,
                    CommunityKey = communityKey,
                    FacilityName = facilityName,
                    IsLocked = isLocked,
                    LastChangedAt = lastChangedAt,
                    AnsweredQuestionCount = answeredCountByKey.GetValueOrDefault(key),
                    TotalQuestionCount = totalQuestionCount,
                    UserDisplayName = !string.IsNullOrWhiteSpace(user?.FullName)
                        ? user!.FullName
                        : user?.Email ?? $"User {userId}"
                });
            }

            return rows
                .OrderBy(r => r.FacilityName)
                .ThenBy(r => r.UserDisplayName)
                .ToList();
        }

        public async Task<bool> SetSurveyLockStateAsync(int userId, int? communityKey, bool isLocked, int actorUserId, UserRole actorRole)
        {
            if (actorRole != UserRole.Admin && actorRole != UserRole.Manager)
            {
                throw new UnauthorizedAccessException("Only admins and managers can change survey lock state.");
            }

            if (actorRole == UserRole.Manager && isLocked)
            {
                throw new UnauthorizedAccessException("Managers can only unlock surveys.");
            }

            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            var status = await GetOrCreateUserSurveyStatusAsync(context, userId, activeYear.Year, communityKey);
            if (status.IsCompleted == isLocked)
            {
                return false;
            }

            status.IsCompleted = isLocked;
            status.UpdatedAt = TimeHelper.CstNow;

            // Log the lock/unlock action
            var actorUser = await context.Users.FindAsync(actorUserId);
            var auditLog = new SurveyLockAuditLog
            {
                UserId = userId,
                ActorUserId = actorUserId,
                ActorDisplayName = actorUser?.FullName ?? "Unknown",
                ActorRole = actorRole,
                SurveyYearId = activeYear.Year,
                CommunityKey = communityKey,
                NewLockState = isLocked,
                ActionAt = TimeHelper.CstNow
            };
            context.SurveyLockAuditLogs.Add(auditLog);

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<List<CompletedSurveyEntry>> GetCompletedSurveyEntriesAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            return await context.UserSurveyStatuses
                .Where(s => s.SurveyYearId == activeYear.Year && s.IsCompleted)
                .Include(s => s.User)
                .OrderByDescending(s => s.UpdatedAt)
                .Select(s => new CompletedSurveyEntry
                {
                    UserId = s.UserId,
                    CommunityKey = s.CommunityKey,
                    SurveyYearId = s.SurveyYearId,
                    UpdatedAt = s.UpdatedAt,
                    UserDisplayName = !string.IsNullOrWhiteSpace(s.User.FullName)
                        ? s.User.FullName
                        : s.User.Email
                })
                .ToListAsync();
        }

        public async Task UpdateSectionAsync(int sectionId, string newName)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);
            var sec = await context.Sections.FirstOrDefaultAsync(s => s.Id == sectionId && s.SurveyYearId == activeYear.Year);
            if (sec != null)
            {
                sec.Name = newName;
                await context.SaveChangesAsync();
            }
        }

        public async Task UpdateQuestionAsync(int questionId, string newText)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);
            var question = await context.Questions.FirstOrDefaultAsync(q => q.Id == questionId && q.SurveyYearId == activeYear.Year);
            if (question != null)
            {
                question.Text = newText;
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteSectionAsync(int sectionId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            var sec = await context.Sections
                .Include(s => s.Questions)
                    .ThenInclude(q => q.Responses)
                .Include(s => s.Subsections)
                .FirstOrDefaultAsync(s => s.Id == sectionId && s.SurveyYearId == activeYear.Year);

            if (sec != null)
            {
                context.Sections.Remove(sec);
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteQuestionAsync(int questionId)
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            var question = await context.Questions
                .Include(q => q.Responses)
                .FirstOrDefaultAsync(q => q.Id == questionId && q.SurveyYearId == activeYear.Year);

            if (question != null)
            {
                context.Questions.Remove(question);
                await context.SaveChangesAsync();
            }
        }

        public async Task<string> ClearAllResponsesWithBackupAsync()
        {
            using var context = await _factory.CreateDbContextAsync();
            var activeYear = await GetOrCreateActiveSurveyYearAsync(context);

            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            // Prepare backup directory under app base
            var backupDirectory = Path.Combine(AppContext.BaseDirectory, "backups");
            Directory.CreateDirectory(backupDirectory);

            string backupFilePath;

            // If using SQL Server, perform a BACKUP DATABASE to a .bak file
            var connTypeName = connection.GetType().Name ?? string.Empty;
            if (connTypeName.IndexOf("SqlConnection", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var dbName = connection.Database;
                var backupFileName = $"{dbName}-backup-{DateTime.Now:yyyyMMdd-HHmmss}.bak";
                backupFilePath = Path.Combine(backupDirectory, backupFileName);

                await using var cmd = connection.CreateCommand();
                cmd.CommandText = $"BACKUP DATABASE [{dbName}] TO DISK = N'{backupFilePath}' WITH INIT;";
                cmd.CommandType = CommandType.Text;
                // Execute backup command on the server; may require SQL Server account permissions
                await cmd.ExecuteNonQueryAsync();
            }
            else
            {
                // Fallback: export responses for the active survey year to a JSON file
                var responses = await context.Responses
                    .Where(r => r.SurveyYearId == activeYear.Year)
                    .ToListAsync();

                var backupFileName = $"responses-backup-{DateTime.Now:yyyyMMdd-HHmms}.json";
                backupFilePath = Path.Combine(backupDirectory, backupFileName);
                await File.WriteAllTextAsync(backupFilePath, System.Text.Json.JsonSerializer.Serialize(responses));
            }

            // Delete responses for the active survey year
            var allResponses = await context.Responses
                .Where(r => r.SurveyYearId == activeYear.Year)
                .ToListAsync();
            context.Responses.RemoveRange(allResponses);

            // Reset user survey statuses for the active survey year
            var statusRows = await context.UserSurveyStatuses
                .Where(s => s.SurveyYearId == activeYear.Year)
                .ToListAsync();

            foreach (var status in statusRows)
            {
                status.IsCompleted = false;
                status.UpdatedAt = TimeHelper.CstNow;
            }

            await context.SaveChangesAsync();

            return backupFilePath;
        }

        private static async Task<SurveyYear> GetOrCreateActiveSurveyYearAsync(AppDbContext context)
        {
            var activeYear = await context.SurveyYears
                .OrderByDescending(e => e.Year)
                .FirstOrDefaultAsync(e => e.Status == SurveyYearStatus.Active);

            if (activeYear != null)
            {
                return activeYear;
            }

            var currentYear = TimeHelper.CstNow.Year;
            var existingForYear = await context.SurveyYears.FirstOrDefaultAsync(e => e.Year == currentYear);
            if (existingForYear != null)
            {
                existingForYear.Status = SurveyYearStatus.Active;
                await context.SaveChangesAsync();
                return existingForYear;
            }

            activeYear = new SurveyYear
            {
                Year = currentYear,
                Status = SurveyYearStatus.Active,
                CreatedAt = TimeHelper.CstNow
            };

            context.SurveyYears.Add(activeYear);
            await context.SaveChangesAsync();
            return activeYear;
        }

        private static async Task<UserSurveyStatus> GetOrCreateUserSurveyStatusAsync(AppDbContext context, int userId, int surveyYearId, int? communityKey)
        {
            var status = await context.UserSurveyStatuses
                .FirstOrDefaultAsync(s => s.UserId == userId && s.SurveyYearId == surveyYearId && s.CommunityKey == communityKey);

            if (status != null)
            {
                return status;
            }

            status = new UserSurveyStatus
            {
                UserId = userId,
                SurveyYearId = surveyYearId,
                CommunityKey = communityKey,
                IsCompleted = false,
                UpdatedAt = TimeHelper.CstNow
            };

            context.UserSurveyStatuses.Add(status);
            await context.SaveChangesAsync();
            return status;
        }

        private static async Task CloneSectionsAndQuestionsAsync(AppDbContext context, int sourceSurveyYearId, int targetSurveyYearId)
        {
            var sourceSections = await context.Sections
                .Where(s => s.SurveyYearId == sourceSurveyYearId)
                .OrderBy(s => s.Id)
                .ToListAsync();

            var sourceQuestions = await context.Questions
                .Where(q => q.SurveyYearId == sourceSurveyYearId)
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
                        SurveyYearId = targetSurveyYearId
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
                            SurveyYearId = targetSurveyYearId
                        });
                    }

                    await context.SaveChangesAsync();
                    await CloneLevelAsync(sourceSection.Id, newSection.Id);
                }
            }

            await CloneLevelAsync(null, null);
        }

        private static string BuildCompletionKey(int userId, int? communityKey)
        {
            return $"{userId}:{communityKey.GetValueOrDefault()}";
        }

        private static (int userId, int? communityKey) ParseCompletionKey(string key)
        {
            var split = key.Split(':', 2);
            if (!int.TryParse(split[0], out var userId))
            {
                return (0, 0);
            }

            if (split.Length < 2 || !int.TryParse(split[1], out var communityKey))
            {
                return (userId, 0);
            }

            return (userId, communityKey);
        }

        private static string ResolveFacilityName(List<Community> communityRows, int userId, int? communityKey)
        {
            Community? match = null;

            if (communityKey.HasValue)
            {
                match = communityRows.FirstOrDefault(c => c.Id == userId && c.CommunityKey == communityKey.Value)
                    ?? communityRows.FirstOrDefault(c => c.CommunityKey == communityKey.Value);
            }

            match ??= communityRows.FirstOrDefault(c => c.Id == userId);

            if (match != null && !string.IsNullOrWhiteSpace(match.Facility))
            {
                return match.Facility;
            }

            return communityKey.HasValue
                ? $"Unknown Facility (CK: {communityKey.Value})"
                : "Unknown Facility";
        }

        private static void FilterResponsesToCompletedStatus(List<Section> sections, HashSet<string> completedKeys)
        {
            foreach (var section in sections)
            {
                foreach (var question in section.Questions)
                {
                    question.Responses = question.Responses
                        .Where(r => completedKeys.Contains(BuildCompletionKey(r.UserId, r.CommunityKey)))
                        .ToList();
                }

                if (section.Subsections.Any())
                {
                    FilterResponsesToCompletedStatus(section.Subsections, completedKeys);
                }
            }
        }

        
    }
}