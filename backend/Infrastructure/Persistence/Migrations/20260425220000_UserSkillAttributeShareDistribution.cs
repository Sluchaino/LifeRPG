using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260425220000_UserSkillAttributeShareDistribution")]
    public partial class UserSkillAttributeShareDistribution : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SharePercent",
                table: "user_skill_attributes",
                type: "integer",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.Sql(
                @"WITH ranked AS (
    SELECT
        ""UserSkillId"",
        ""AttributeType"",
        COUNT(*) OVER (PARTITION BY ""UserSkillId"") AS cnt,
        ROW_NUMBER() OVER (PARTITION BY ""UserSkillId"" ORDER BY ""AttributeType"") AS rn
    FROM user_skill_attributes
),
base AS (
    SELECT
        ""UserSkillId"",
        ""AttributeType"",
        cnt,
        rn,
        FLOOR(100.0 / cnt)::int AS base_share
    FROM ranked
),
computed AS (
    SELECT
        ""UserSkillId"",
        ""AttributeType"",
        CASE
            WHEN cnt = 1 THEN 100
            WHEN rn < cnt THEN base_share
            ELSE 100 - base_share * (cnt - 1)
        END AS share_percent
    FROM base
)
UPDATE user_skill_attributes AS usa
SET ""SharePercent"" = computed.share_percent
FROM computed
WHERE usa.""UserSkillId"" = computed.""UserSkillId""
  AND usa.""AttributeType"" = computed.""AttributeType"";");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_skill_attributes_share_percent_range",
                table: "user_skill_attributes",
                sql: "\"SharePercent\" > 0 AND \"SharePercent\" <= 100");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_skill_attributes_share_percent_range",
                table: "user_skill_attributes");

            migrationBuilder.DropColumn(
                name: "SharePercent",
                table: "user_skill_attributes");
        }
    }
}
