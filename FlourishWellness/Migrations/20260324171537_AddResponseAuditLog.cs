using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlourishWellness.Migrations
{
    /// <inheritdoc />
    public partial class AddResponseAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Users",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SAMAccountName",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "ParentSectionId",
                table: "Sections",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Sections",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Sections",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "SurveyYear",
                table: "Sections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Responses",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "QuestionId",
                table: "Responses",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "Answer",
                table: "Responses",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Responses",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "CommunityKey",
                table: "Responses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreateDate",
                table: "Responses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Modified",
                table: "Responses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SAMAccountName",
                table: "Responses",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SurveyYear",
                table: "Responses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Questions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "Questions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Questions",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER")
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<int>(
                name: "SurveyYear",
                table: "Questions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Community",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CommunityKey = table.Column<int>(type: "int", nullable: false),
                    SAMAccountName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Facility = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Community", x => new { x.UserId, x.CommunityKey });
                    table.ForeignKey(
                        name: "FK_Community_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResponseAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ResponseId = table.Column<int>(type: "int", nullable: false),
                    QuestionId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SAMAccountName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NewAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SurveyYear",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyYear", x => x.Id);
                    table.UniqueConstraint("AK_SurveyYear_Year", x => x.Year);
                });

            migrationBuilder.CreateTable(
                name: "UserSurveyStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SurveyYear = table.Column<int>(type: "int", nullable: false),
                    CommunityKey = table.Column<int>(type: "int", nullable: true),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSurveyStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSurveyStatuses_SurveyYear_SurveyYear",
                        column: x => x.SurveyYear,
                        principalTable: "SurveyYear",
                        principalColumn: "Year",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSurveyStatuses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sections_SurveyYear",
                table: "Sections",
                column: "SurveyYear");

            migrationBuilder.CreateIndex(
                name: "IX_Responses_SurveyYear",
                table: "Responses",
                column: "SurveyYear");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SurveyYear",
                table: "Questions",
                column: "SurveyYear");

            migrationBuilder.CreateIndex(
                name: "IX_SurveyYear_Year",
                table: "SurveyYear",
                column: "Year",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSurveyStatuses_SurveyYear",
                table: "UserSurveyStatuses",
                column: "SurveyYear");

            migrationBuilder.CreateIndex(
                name: "IX_UserSurveyStatuses_UserId_SurveyYear_CommunityKey",
                table: "UserSurveyStatuses",
                columns: new[] { "UserId", "SurveyYear", "CommunityKey" },
                unique: true,
                filter: "[CommunityKey] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_SurveyYear_SurveyYear",
                table: "Questions",
                column: "SurveyYear",
                principalTable: "SurveyYear",
                principalColumn: "Year",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Responses_SurveyYear_SurveyYear",
                table: "Responses",
                column: "SurveyYear",
                principalTable: "SurveyYear",
                principalColumn: "Year",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sections_SurveyYear_SurveyYear",
                table: "Sections",
                column: "SurveyYear",
                principalTable: "SurveyYear",
                principalColumn: "Year",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Questions_SurveyYear_SurveyYear",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_Responses_SurveyYear_SurveyYear",
                table: "Responses");

            migrationBuilder.DropForeignKey(
                name: "FK_Sections_SurveyYear_SurveyYear",
                table: "Sections");

            migrationBuilder.DropTable(
                name: "Community");

            migrationBuilder.DropTable(
                name: "ResponseAuditLogs");

            migrationBuilder.DropTable(
                name: "UserSurveyStatuses");

            migrationBuilder.DropTable(
                name: "SurveyYear");

            migrationBuilder.DropIndex(
                name: "IX_Sections_SurveyYear",
                table: "Sections");

            migrationBuilder.DropIndex(
                name: "IX_Responses_SurveyYear",
                table: "Responses");

            migrationBuilder.DropIndex(
                name: "IX_Questions_SurveyYear",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SAMAccountName",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SurveyYear",
                table: "Sections");

            migrationBuilder.DropColumn(
                name: "CommunityKey",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "CreateDate",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "Modified",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "SAMAccountName",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "SurveyYear",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "SurveyYear",
                table: "Questions");

            migrationBuilder.AlterColumn<int>(
                name: "Role",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedAt",
                table: "Users",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Users",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "ParentSectionId",
                table: "Sections",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Sections",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Sections",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Responses",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "QuestionId",
                table: "Responses",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Answer",
                table: "Responses",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Responses",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Questions",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "Questions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Questions",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int")
                .OldAnnotation("SqlServer:Identity", "1, 1");
        }
    }
}
