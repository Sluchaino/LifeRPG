using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CalendarTaskAttributeShareDistribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SharePercent",
                table: "calendar_task_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddCheckConstraint(
                name: "ck_calendar_task_attributes_share_percent_range",
                table: "calendar_task_attributes",
                sql: "\"SharePercent\" > 0 AND \"SharePercent\" <= 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_calendar_task_attributes_share_percent_range",
                table: "calendar_task_attributes");

            migrationBuilder.DropColumn(
                name: "SharePercent",
                table: "calendar_task_attributes");
        }
    }
}
