using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CalendarTaskSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_task_skills",
                columns: table => new
                {
                    CalendarTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserSkillId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_calendar_task_skills", x => new { x.CalendarTaskId, x.UserSkillId });
                    table.ForeignKey(
                        name: "FK_calendar_task_skills_calendar_tasks_CalendarTaskId",
                        column: x => x.CalendarTaskId,
                        principalTable: "calendar_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_calendar_task_skills_user_skills_UserSkillId",
                        column: x => x.UserSkillId,
                        principalTable: "user_skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_calendar_task_skills_UserSkillId",
                table: "calendar_task_skills",
                column: "UserSkillId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_task_skills");
        }
    }
}
