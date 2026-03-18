-- ============================================================
-- FlourishWellness Production Schema
-- Server: ASISQLDBPROD
-- Database: FlourishWellness
-- Last updated: 2026-03-18
-- ============================================================

-- Survey years (one row per annual survey cycle)
CREATE TABLE dbo.SurveyYear (
    Id          INT            NOT NULL IDENTITY(1,1),
    Year        INT            NOT NULL,
    Status      INT            NOT NULL,   -- 1 = Archived, 2 = Active
    CreatedAt   DATETIME2      NOT NULL,
    CONSTRAINT PK_SurveyYear PRIMARY KEY (Id),
    CONSTRAINT UQ_SurveyYear_Year UNIQUE (Year)
);

-- Top-level and nested survey sections
CREATE TABLE dbo.Sections (
    Id              INT            NOT NULL IDENTITY(1,1),
    Name            NVARCHAR(MAX)  NOT NULL,
    SurveyYear      INT            NOT NULL,   -- FK → dbo.SurveyYear.Id
    ParentSectionId INT            NULL,        -- NULL = top-level section
    CONSTRAINT PK_Sections PRIMARY KEY (Id),
    CONSTRAINT FK_Sections_SurveyYear    FOREIGN KEY (SurveyYear)      REFERENCES dbo.SurveyYear (Id),
    CONSTRAINT FK_Sections_ParentSection FOREIGN KEY (ParentSectionId) REFERENCES dbo.Sections   (Id)
);

CREATE INDEX IX_Sections_SurveyYear      ON dbo.Sections (SurveyYear);
CREATE INDEX IX_Sections_ParentSectionId ON dbo.Sections (ParentSectionId);

-- Individual survey questions belonging to a section
CREATE TABLE dbo.Questions (
    Id         INT            NOT NULL IDENTITY(1,1),
    Text       NVARCHAR(MAX)  NOT NULL,
    SurveyYear INT            NOT NULL,   -- FK → dbo.SurveyYear.Id
    SectionId  INT            NOT NULL,   -- FK → dbo.Sections.Id
    CONSTRAINT PK_Questions PRIMARY KEY (Id),
    CONSTRAINT FK_Questions_SurveyYear FOREIGN KEY (SurveyYear) REFERENCES dbo.SurveyYear (Id),
    CONSTRAINT FK_Questions_Section    FOREIGN KEY (SectionId)  REFERENCES dbo.Sections   (Id)
);

CREATE INDEX IX_Questions_SurveyYear ON dbo.Questions (SurveyYear);
CREATE INDEX IX_Questions_SectionId  ON dbo.Questions (SectionId);

-- Application users (populated automatically on first Windows/AD login)
CREATE TABLE dbo.Users (
    Id               INT            NOT NULL IDENTITY(1,1),
    Email            NVARCHAR(256)  NOT NULL,
    FullName         NVARCHAR(256)  NOT NULL,
    SAMAccountName   NVARCHAR(256)  NOT NULL,
    Role             INT            NOT NULL,   -- 1 = Employee, 2 = Manager, 3 = Admin
    CreatedAt        DATETIME2      NOT NULL,
    CONSTRAINT PK_Users PRIMARY KEY (Id),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);

-- Per-user survey responses
CREATE TABLE dbo.Responses (
    Id             INT            NOT NULL IDENTITY(1,1),
    Answer         NVARCHAR(MAX)  NOT NULL,
    SurveyYear     INT            NOT NULL,   -- FK → dbo.SurveyYear.Id
    QuestionId     INT            NOT NULL,   -- FK → dbo.Questions.Id
    UserId         INT            NOT NULL,   -- FK → dbo.Users.Id
    SAMAccountName NVARCHAR(256)  NOT NULL,
    CreateDate     DATETIME2      NOT NULL,
    Modified       DATETIME2      NULL,
    CommunityKey   NVARCHAR(256)  NULL,
    CONSTRAINT PK_Responses PRIMARY KEY (Id),
    CONSTRAINT FK_Responses_SurveyYear FOREIGN KEY (SurveyYear) REFERENCES dbo.SurveyYear (Id),
    CONSTRAINT FK_Responses_Question   FOREIGN KEY (QuestionId) REFERENCES dbo.Questions  (Id),
    CONSTRAINT FK_Responses_User       FOREIGN KEY (UserId)     REFERENCES dbo.Users      (Id)
);

CREATE INDEX IX_Responses_SurveyYear ON dbo.Responses (SurveyYear);
CREATE INDEX IX_Responses_QuestionId ON dbo.Responses (QuestionId);
CREATE INDEX IX_Responses_UserId     ON dbo.Responses (UserId);

-- Tracks whether a user has submitted for a given survey year
-- Note: SurveyEntityId is the physical column name for the SurveyYear FK in this table.
CREATE TABLE dbo.UserSurveyStatuses (
    Id             INT       NOT NULL IDENTITY(1,1),
    UserId         INT       NOT NULL,   -- FK → dbo.Users.Id
    SurveyEntityId INT       NOT NULL,   -- FK → dbo.SurveyYear.Id
    IsCompleted    BIT       NOT NULL,
    UpdatedAt      DATETIME2 NOT NULL,
    CONSTRAINT PK_UserSurveyStatuses PRIMARY KEY (Id),
    CONSTRAINT FK_UserSurveyStatuses_User       FOREIGN KEY (UserId)         REFERENCES dbo.Users     (Id),
    CONSTRAINT FK_UserSurveyStatuses_SurveyYear FOREIGN KEY (SurveyEntityId) REFERENCES dbo.SurveyYear(Id),
    CONSTRAINT UQ_UserSurveyStatuses_User_Year  UNIQUE (UserId, SurveyEntityId)
);

-- Local cache of facility/community data sourced from AmericareDW.dbo.FlourishADUsers
CREATE TABLE dbo.Community (
    Id             INT            NOT NULL IDENTITY(1,1),
    SAMAccountName NVARCHAR(256)  NOT NULL,
    Facility       NVARCHAR(256)  NOT NULL,
    CommunityKey   NVARCHAR(256)  NULL,
    CONSTRAINT PK_Community PRIMARY KEY (Id)
);
