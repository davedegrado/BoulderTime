using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610121808 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcement_dismissals",
                schema: "bouldertime",
                columns: table => new
                {
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcement_dismissals", x => new { x.user_id, x.announcement_id });
                    table.ForeignKey(
                        name: "fk_announcement_dismissals_gym_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalSchema: "bouldertime",
                        principalTable: "gym_announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_announcement_dismissals_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_announcement_dismissals_announcement_id",
                schema: "bouldertime",
                table: "announcement_dismissals",
                column: "announcement_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement_dismissals",
                schema: "bouldertime");
        }
    }
}
