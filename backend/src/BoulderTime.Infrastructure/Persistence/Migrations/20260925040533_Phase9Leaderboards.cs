using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase9Leaderboards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "leaderboard_excluded_at",
                schema: "bouldertime",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "leaderboard_excluded_by_user_id",
                schema: "bouldertime",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "leaderboard_opt_out",
                schema: "bouldertime",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "facebook_url",
                schema: "bouldertime",
                table: "gyms",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "instagram_url",
                schema: "bouldertime",
                table: "gyms",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "leaderboard_reports",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    handled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    handled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    handling_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leaderboard_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_leaderboard_reports_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_leaderboard_reports_users_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_leaderboard_reports_users_reported_user_id",
                        column: x => x.reported_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_leaderboard_reports_gym_id_reported_user_id",
                schema: "bouldertime",
                table: "leaderboard_reports",
                columns: new[] { "gym_id", "reported_user_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_leaderboard_reports_reported_by_user_id",
                schema: "bouldertime",
                table: "leaderboard_reports",
                column: "reported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_leaderboard_reports_reported_user_id",
                schema: "bouldertime",
                table: "leaderboard_reports",
                column: "reported_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_leaderboard_reports_status",
                schema: "bouldertime",
                table: "leaderboard_reports",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "leaderboard_reports",
                schema: "bouldertime");

            migrationBuilder.DropColumn(
                name: "leaderboard_excluded_at",
                schema: "bouldertime",
                table: "users");

            migrationBuilder.DropColumn(
                name: "leaderboard_excluded_by_user_id",
                schema: "bouldertime",
                table: "users");

            migrationBuilder.DropColumn(
                name: "leaderboard_opt_out",
                schema: "bouldertime",
                table: "users");

            migrationBuilder.DropColumn(
                name: "facebook_url",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "instagram_url",
                schema: "bouldertime",
                table: "gyms");
        }
    }
}
