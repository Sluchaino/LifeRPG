using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CalendarTaskHabitLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HabitId",
                table: "calendar_tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_calendar_tasks_HabitId",
                table: "calendar_tasks",
                column: "HabitId");

            migrationBuilder.AddForeignKey(
                name: "FK_calendar_tasks_habits_HabitId",
                table: "calendar_tasks",
                column: "HabitId",
                principalTable: "habits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_calendar_tasks_habits_HabitId",
                table: "calendar_tasks");

            migrationBuilder.DropIndex(
                name: "IX_calendar_tasks_HabitId",
                table: "calendar_tasks");

            migrationBuilder.DropColumn(
                name: "HabitId",
                table: "calendar_tasks");
        }
    }
}
