using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610101901 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_boulder_attempts_boulder_id",
                schema: "bouldertime",
                table: "boulder_attempts");

            migrationBuilder.AddColumn<string>(
                name: "intensity",
                schema: "bouldertime",
                table: "boulders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "gym_tags",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gym_tags", x => x.id);
                    table.ForeignKey(
                        name: "fk_gym_tags_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "boulder_tags",
                schema: "bouldertime",
                columns: table => new
                {
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boulder_tags", x => new { x.boulder_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_boulder_tags_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulder_tags_gym_tags_tag_id",
                        column: x => x.tag_id,
                        principalSchema: "bouldertime",
                        principalTable: "gym_tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_boulder_attempts_boulder_id_completed_completed_at",
                schema: "bouldertime",
                table: "boulder_attempts",
                columns: new[] { "boulder_id", "completed", "completed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_boulder_tags_tag_id",
                schema: "bouldertime",
                table: "boulder_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_gym_tags_gym_id_normalized_name",
                schema: "bouldertime",
                table: "gym_tags",
                columns: new[] { "gym_id", "normalized_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boulder_tags",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "gym_tags",
                schema: "bouldertime");

            migrationBuilder.DropIndex(
                name: "ix_boulder_attempts_boulder_id_completed_completed_at",
                schema: "bouldertime",
                table: "boulder_attempts");

            migrationBuilder.DropColumn(
                name: "intensity",
                schema: "bouldertime",
                table: "boulders");

            migrationBuilder.CreateIndex(
                name: "ix_boulder_attempts_boulder_id",
                schema: "bouldertime",
                table: "boulder_attempts",
                column: "boulder_id");
        }
    }
}
