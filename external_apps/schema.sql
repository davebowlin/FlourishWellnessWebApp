-- ============================================================
-- FlourishWellness Production Schema
-- Server: ASISQLDBPROD
-- Database: FlourishWellness
-- Last updated: 2026-03-18
-- ============================================================
-- The schema below should now match the full SQL Server
-- database schema for the FlourishWellness app in production.
-- ============================================================

-- Survey years (one row per annual survey cycle)
-- PK constraint name in production: PK_SurveyEntities
CREATE TABLE dbo.SurveyYear (
    Id        INT       NOT NULL IDENTITY(1,1),
    Year      INT       NOT NULL,
    Status    INT       NOT NULL,   -- 1 = Archived, 2 = Active
    CreatedAt DATETIME2 NOT NULL,
    CONSTRAINT PK_SurveyEntities PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_SurveyEntities_Year ON dbo.SurveyYear (Year);

-- Top-level and nested survey sections
-- SurveyYear column maps to EF property SurveyYearId; default value (0) set by EF migrations
CREATE TABLE dbo.Sections (
    Id              INT           NOT NULL IDENTITY(1,1),
    Name            NVARCHAR(255) NOT NULL,
    SurveyYear      INT           NOT NULL DEFAULT ((0)),  -- FK → dbo.SurveyYear.Id
    ParentSectionId INT           NULL,                    -- NULL = top-level section
    CONSTRAINT PK_Sections PRIMARY KEY (Id),
    CONSTRAINT FK_Sections_Sections_ParentSectionId FOREIGN KEY (ParentSectionId) REFERENCES dbo.Sections (Id)
);

CREATE INDEX IX_Sections_SurveyEntityId  ON dbo.Sections (SurveyYear);
CREATE INDEX IX_Sections_ParentSectionId ON dbo.Sections (ParentSectionId);

-- Individual survey questions belonging to a section
-- SurveyYear column maps to EF property SurveyYearId; default value (0) set by EF migrations
CREATE TABLE dbo.Questions (
    Id        INT          NOT NULL IDENTITY(1,1),
    Text      NVARCHAR(MAX) NOT NULL,
    SectionId INT          NOT NULL,   -- FK → dbo.Sections.Id
    SurveyYear INT         NOT NULL DEFAULT ((0)),  -- FK → dbo.SurveyYear.Id
    CONSTRAINT PK_Questions PRIMARY KEY (Id),
    CONSTRAINT FK_Questions_Sections_SectionId FOREIGN KEY (SectionId) REFERENCES dbo.Sections (Id) ON DELETE CASCADE
);

CREATE INDEX IX_Questions_SectionId      ON dbo.Questions (SectionId);
CREATE INDEX IX_Questions_SurveyEntityId ON dbo.Questions (SurveyYear);

-- Application users (populated automatically on first Windows/AD login)
-- Role: 1 = Employee, 2 = Manager, 3 = Admin
-- FullName and SAMAccountName default to empty string per EF migrations
CREATE TABLE dbo.Users (
    Id             INT           NOT NULL IDENTITY(1,1),
    Email          NVARCHAR(255) NOT NULL,
    Role           INT           NOT NULL,
    CreatedAt      DATETIME2     NOT NULL,
    FullName       NVARCHAR(256) NOT NULL DEFAULT (''),
    SAMAccountName NVARCHAR(256) NOT NULL DEFAULT (''),
    CONSTRAINT PK_Users PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users (Email);

-- Per-user survey responses
-- SurveyYear column maps to EF property SurveyYearId; default value (0) set by EF migrations
-- SAMaccountName casing matches production column exactly
-- CommunityKey is an int FK reference to dbo.Community
CREATE TABLE dbo.Responses (
    Id             INT           NOT NULL IDENTITY(1,1),
    Answer         NVARCHAR(MAX) NOT NULL,
    QuestionId     INT           NOT NULL,   -- FK → dbo.Questions.Id
    UserId         INT           NOT NULL,   -- FK → dbo.Users.Id
    SurveyYear     INT           NOT NULL DEFAULT ((0)),  -- FK → dbo.SurveyYear.Id
    SAMaccountName NVARCHAR(256) NULL,
    CreateDate     DATETIME2     NULL,
    Modified       DATETIME2     NULL,
    CommunityKey   INT           NULL,       -- 0 = no Community/AD entry; NULL should not occur
    CONSTRAINT PK_Responses PRIMARY KEY (Id),
    CONSTRAINT FK_Responses_Questions_QuestionId FOREIGN KEY (QuestionId) REFERENCES dbo.Questions (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Responses_Users_UserId         FOREIGN KEY (UserId)     REFERENCES dbo.Users     (Id) ON DELETE CASCADE
);

CREATE INDEX IX_Responses_QuestionId    ON dbo.Responses (QuestionId);
CREATE INDEX IX_Responses_UserId        ON dbo.Responses (UserId);
CREATE INDEX IX_Responses_SurveyEntityId ON dbo.Responses (SurveyYear);

-- Tracks whether a user has submitted for a given survey year
-- SurveyYear column maps to EF property SurveyYearId (HasColumnName("SurveyYear") in AppDbContext)
CREATE TABLE dbo.UserSurveyStatuses (
    Id          INT       NOT NULL IDENTITY(1,1),
    UserId      INT       NOT NULL,   -- FK → dbo.Users.Id
    SurveyYear  INT       NOT NULL,   -- FK → dbo.SurveyYear.Id
    CommunityKey INT      NULL,       -- Scopes completion per facility/community; 0 = no Community/AD entry
    IsCompleted BIT       NOT NULL,
    UpdatedAt   DATETIME2 NOT NULL,
    CONSTRAINT PK_UserSurveyStatuses PRIMARY KEY (Id),
    CONSTRAINT FK_UserSurveyStatuses_Users_UserId                       FOREIGN KEY (UserId)    REFERENCES dbo.Users     (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserSurveyStatuses_SurveyEntities_SurveyEntityId      FOREIGN KEY (SurveyYear) REFERENCES dbo.SurveyYear (Id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IX_UserSurveyStatuses_UserId_SurveyEntityId ON dbo.UserSurveyStatuses (UserId, SurveyYear, CommunityKey);

-- Local cache of facility/community data sourced from AmericareDW.dbo.FlourishADUsers
-- Note: The identity PK column is named UserId (not Id) in production.
--       EF maps Community.Id (C# property) to this column via HasColumnName("UserId").
-- CommunityKey is an int matching the int type in AmericareDW.
CREATE TABLE dbo.Community (
    UserId         INT           NOT NULL IDENTITY(1,1),
    SAMAccountName NVARCHAR(256) NOT NULL,
    Facility       NVARCHAR(256) NOT NULL,
    CommunityKey   INT           NOT NULL,
    CONSTRAINT PK_Community PRIMARY KEY (UserId)
);
