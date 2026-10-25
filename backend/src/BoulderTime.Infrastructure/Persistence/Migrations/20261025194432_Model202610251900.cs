using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610251900 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "launched_at",
                schema: "bouldertime",
                table: "gyms",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "activity_days",
                schema: "bouldertime",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_days", x => new { x.user_id, x.day });
                    table.ForeignKey(
                        name: "fk_activity_days_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "badge_awards",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    badge_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scope_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: true),
                    period_kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    period_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    earned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    featured_order = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_badge_awards", x => x.id);
                    table.CheckConstraint("ck_badge_awards_featured_order", "featured_order IS NULL OR featured_order BETWEEN 1 AND 3");
                    table.ForeignKey(
                        name: "fk_badge_awards_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_badge_awards_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "competition_periods",
                schema: "bouldertime",
                columns: table => new
                {
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    champions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_competition_periods", x => new { x.gym_id, x.kind, x.start });
                    table.ForeignKey(
                        name: "fk_competition_periods_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_badge_awards_badge_id_scope_key",
                schema: "bouldertime",
                table: "badge_awards",
                columns: new[] { "badge_id", "scope_key" });

            migrationBuilder.CreateIndex(
                name: "ix_badge_awards_gym_id",
                schema: "bouldertime",
                table: "badge_awards",
                column: "gym_id");

            migrationBuilder.CreateIndex(
                name: "ix_badge_awards_user_id_badge_id_scope_key",
                schema: "bouldertime",
                table: "badge_awards",
                columns: new[] { "user_id", "badge_id", "scope_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_days",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "badge_awards",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "competition_periods",
                schema: "bouldertime");

            migrationBuilder.DropColumn(
                name: "launched_at",
                schema: "bouldertime",
                table: "gyms");
        }
    }
}
