using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610081139 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "suspended_at",
                schema: "bouldertime",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "suspended_by_user_id",
                schema: "bouldertime",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suspension_reason",
                schema: "bouldertime",
                table: "users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_reports",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    handled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    handled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    handling_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_reports_users_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_reports_users_reported_user_id",
                        column: x => x.reported_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_reports_reported_by_user_id_reported_user_id",
                schema: "bouldertime",
                table: "user_reports",
                columns: new[] { "reported_by_user_id", "reported_user_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_user_reports_reported_user_id",
                schema: "bouldertime",
                table: "user_reports",
                column: "reported_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_reports_status",
                schema: "bouldertime",
                table: "user_reports",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_reports",
                schema: "bouldertime");

            migrationBuilder.DropColumn(
                name: "suspended_at",
                schema: "bouldertime",
                table: "users");

            migrationBuilder.DropColumn(
                name: "suspended_by_user_id",
                schema: "bouldertime",
                table: "users");

            migrationBuilder.DropColumn(
                name: "suspension_reason",
                schema: "bouldertime",
                table: "users");
        }
    }
}
