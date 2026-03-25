using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FlourishWellness.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyLockAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SurveyLockAuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ActorUserId = table.Column<int>(type: "int", nullable: false),
                    ActorDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActorRole = table.Column<int>(type: "int", nullable: false),
                    SurveyYearId = table.Column<int>(type: "int", nullable: false),
                    CommunityKey = table.Column<int>(type: "int", nullable: true),
                    NewLockState = table.Column<bool>(type: "bit", nullable: false),
                    ActionAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyLockAuditLogs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurveyLockAuditLogs");
        }
    }
}
