using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610251000 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boulder_projects",
                schema: "bouldertime",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boulder_projects", x => new { x.user_id, x.boulder_id });
                    table.ForeignKey(
                        name: "fk_boulder_projects_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulder_projects_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_boulder_projects_boulder_id",
                schema: "bouldertime",
                table: "boulder_projects",
                column: "boulder_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boulder_projects",
                schema: "bouldertime");
        }
    }
}
