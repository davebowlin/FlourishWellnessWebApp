BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS "SurveyEntities" (
	"Id" INTEGER NOT NULL,
	"Year" INTEGER NOT NULL,
	"Status" INTEGER NOT NULL,
	"CreatedAt" TEXT NOT NULL,
	CONSTRAINT "PK_SurveyEntities" PRIMARY KEY("Id" AUTOINCREMENT)
);

CREATE TABLE IF NOT EXISTS "Sections" (
	"Id" INTEGER NOT NULL,
	"Name" TEXT NOT NULL,
	"SurveyEntityId" INTEGER NOT NULL,
	"ParentSectionId" INTEGER,
	CONSTRAINT "PK_Sections" PRIMARY KEY("Id" AUTOINCREMENT),
	CONSTRAINT "FK_Sections_SurveyEntities_SurveyEntityId" FOREIGN KEY("SurveyEntityId") REFERENCES "SurveyEntities"("Id") ON DELETE CASCADE,
	CONSTRAINT "FK_Sections_Sections_ParentSectionId" FOREIGN KEY("ParentSectionId") REFERENCES "Sections"("Id") ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS "Questions" (
	"Id" INTEGER NOT NULL,
	"Text" TEXT NOT NULL,
	"SurveyEntityId" INTEGER NOT NULL,
	"SectionId" INTEGER NOT NULL,
	CONSTRAINT "PK_Questions" PRIMARY KEY("Id" AUTOINCREMENT),
	CONSTRAINT "FK_Questions_SurveyEntities_SurveyEntityId" FOREIGN KEY("SurveyEntityId") REFERENCES "SurveyEntities"("Id") ON DELETE CASCADE,
	CONSTRAINT "FK_Questions_Sections_SectionId" FOREIGN KEY("SectionId") REFERENCES "Sections"("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "Users" (
	"Id" INTEGER NOT NULL,
	"Email" TEXT NOT NULL,
	"FullName" TEXT NOT NULL,
	"PasswordHash" TEXT NOT NULL,
	"Role" INTEGER NOT NULL,
	"IsSurveyCompleted" INTEGER NOT NULL DEFAULT 0,
	"CreatedAt" TEXT NOT NULL,
	CONSTRAINT "PK_Users" PRIMARY KEY("Id" AUTOINCREMENT)
);

CREATE TABLE IF NOT EXISTS "Responses" (
	"Id" INTEGER NOT NULL,
	"Answer" TEXT NOT NULL,
	"SurveyEntityId" INTEGER NOT NULL,
	"QuestionId" INTEGER NOT NULL,
	"UserId" INTEGER NOT NULL,
	CONSTRAINT "PK_Responses" PRIMARY KEY("Id" AUTOINCREMENT),
	CONSTRAINT "FK_Responses_SurveyEntities_SurveyEntityId" FOREIGN KEY("SurveyEntityId") REFERENCES "SurveyEntities"("Id") ON DELETE CASCADE,
	CONSTRAINT "FK_Responses_Questions_QuestionId" FOREIGN KEY("QuestionId") REFERENCES "Questions"("Id") ON DELETE CASCADE,
	CONSTRAINT "FK_Responses_Users_UserId" FOREIGN KEY("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "UserSurveyStatuses" (
	"Id" INTEGER NOT NULL,
	"UserId" INTEGER NOT NULL,
	"SurveyEntityId" INTEGER NOT NULL,
	"IsCompleted" INTEGER NOT NULL,
	"UpdatedAt" TEXT NOT NULL,
	CONSTRAINT "PK_UserSurveyStatuses" PRIMARY KEY("Id" AUTOINCREMENT),
	CONSTRAINT "FK_UserSurveyStatuses_Users_UserId" FOREIGN KEY("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE,
	CONSTRAINT "FK_UserSurveyStatuses_SurveyEntities_SurveyEntityId" FOREIGN KEY("SurveyEntityId") REFERENCES "SurveyEntities"("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
	"MigrationId" TEXT NOT NULL,
	"ProductVersion" TEXT NOT NULL,
	CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY("MigrationId")
);

CREATE TABLE IF NOT EXISTS "__EFMigrationsLock" (
	"Id" INTEGER NOT NULL,
	"Timestamp" TEXT NOT NULL,
	CONSTRAINT "PK___EFMigrationsLock" PRIMARY KEY("Id")
);

CREATE INDEX IF NOT EXISTS "IX_Sections_ParentSectionId" ON "Sections" ("ParentSectionId");
CREATE INDEX IF NOT EXISTS "IX_Sections_SurveyEntityId" ON "Sections" ("SurveyEntityId");
CREATE INDEX IF NOT EXISTS "IX_Questions_SectionId" ON "Questions" ("SectionId");
CREATE INDEX IF NOT EXISTS "IX_Questions_SurveyEntityId" ON "Questions" ("SurveyEntityId");
CREATE INDEX IF NOT EXISTS "IX_Responses_QuestionId" ON "Responses" ("QuestionId");
CREATE INDEX IF NOT EXISTS "IX_Responses_UserId" ON "Responses" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Responses_SurveyEntityId" ON "Responses" ("SurveyEntityId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SurveyEntities_Year" ON "SurveyEntities" ("Year");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserSurveyStatuses_UserId_SurveyEntityId" ON "UserSurveyStatuses" ("UserId", "SurveyEntityId");

COMMIT;
