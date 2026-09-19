using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8Partners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_founding_gym",
                schema: "bouldertime",
                table: "gyms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "early_partnerships",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_early_partnerships", x => x.id);
                    table.ForeignKey(
                        name: "fk_early_partnerships_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_early_partnerships_users_granted_by_user_id",
                        column: x => x.granted_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gyms_is_founding_gym",
                schema: "bouldertime",
                table: "gyms",
                column: "is_founding_gym",
                unique: true,
                filter: "is_founding_gym");

            migrationBuilder.CreateIndex(
                name: "ix_early_partnerships_granted_by_user_id",
                schema: "bouldertime",
                table: "early_partnerships",
                column: "granted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_early_partnerships_gym_id",
                schema: "bouldertime",
                table: "early_partnerships",
                column: "gym_id",
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_early_partnerships_gym_id_started_at",
                schema: "bouldertime",
                table: "early_partnerships",
                columns: new[] { "gym_id", "started_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "early_partnerships",
                schema: "bouldertime");

            migrationBuilder.DropIndex(
                name: "ix_gyms_is_founding_gym",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "is_founding_gym",
                schema: "bouldertime",
                table: "gyms");
        }
    }
}
