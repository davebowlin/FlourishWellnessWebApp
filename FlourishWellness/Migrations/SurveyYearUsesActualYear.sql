-- DO NOT USE THIS; MIGRATION IS COMPLETED AT THIS TIME
--------------------------------------------------------------------------------

-- =============================================================================
-- SurveyYear updates to use actual Year value instead of SurveyYear.Year
-- =============================================================================

USE [FlourishWellness];
GO

ALTER TABLE dbo.UserSurveyStatuses
    DROP CONSTRAINT FK_UserSurveyStatuses_SurveyEntities_SurveyEntityId;
GO

UPDATE s
SET s.SurveyYear = sy.Year
FROM dbo.Sections s
INNER JOIN dbo.SurveyYear sy ON sy.Id = s.SurveyYear;
GO

UPDATE q
SET q.SurveyYear = sy.Year
FROM dbo.Questions q
INNER JOIN dbo.SurveyYear sy ON sy.Id = q.SurveyYear;
GO

UPDATE r
SET r.SurveyYear = sy.Year
FROM dbo.Responses r
INNER JOIN dbo.SurveyYear sy ON sy.Id = r.SurveyYear;
GO

UPDATE uss
SET uss.SurveyYear = sy.Year
FROM dbo.UserSurveyStatuses uss
INNER JOIN dbo.SurveyYear sy ON sy.Id = uss.SurveyYear;
GO

ALTER TABLE dbo.UserSurveyStatuses
    ADD CONSTRAINT FK_UserSurveyStatuses_SurveyEntities_SurveyEntityId
        FOREIGN KEY (SurveyYear) REFERENCES dbo.SurveyYear (Year) ON DELETE CASCADE;
GO
