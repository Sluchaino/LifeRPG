using System;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260425130000_CalendarTaskImportanceAndExperience")]
    public partial class CalendarTaskImportanceAndExperience : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "calendar_tasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExperienceAwarded",
                table: "calendar_tasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Importance",
                table: "calendar_tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Optional");

            migrationBuilder.AddColumn<bool>(
                name: "IsFirstTaskBonusApplied",
                table: "calendar_tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "calendar_tasks");

            migrationBuilder.DropColumn(
                name: "ExperienceAwarded",
                table: "calendar_tasks");

            migrationBuilder.DropColumn(
                name: "Importance",
                table: "calendar_tasks");

            migrationBuilder.DropColumn(
                name: "IsFirstTaskBonusApplied",
                table: "calendar_tasks");
        }
    }
}
