using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase4Climbing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boulder_attempts",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boulder_attempts", x => x.id);
                    table.CheckConstraint("ck_boulder_attempts_attempts", "attempts >= 0 AND attempts <= 999");
                    table.ForeignKey(
                        name: "fk_boulder_attempts_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulder_attempts_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "boulder_follows",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notifications_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boulder_follows", x => x.id);
                    table.ForeignKey(
                        name: "fk_boulder_follows_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_boulder_follows_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "boulder_ratings",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boulder_ratings", x => x.id);
                    table.CheckConstraint("ck_boulder_ratings_rating", "rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_boulder_ratings_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulder_ratings_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gym_follows",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_favorite = table.Column<bool>(type: "boolean", nullable: false),
                    notifications_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gym_follows", x => x.id);
                    table.ForeignKey(
                        name: "fk_gym_follows_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_gym_follows_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sector_follows",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notifications_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sector_follows", x => x.id);
                    table.ForeignKey(
                        name: "fk_sector_follows_sectors_sector_id",
                        column: x => x.sector_id,
                        principalSchema: "bouldertime",
                        principalTable: "sectors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_sector_follows_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_boulder_attempts_boulder_id",
                schema: "bouldertime",
                table: "boulder_attempts",
                column: "boulder_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulder_attempts_user_id_boulder_id",
                schema: "bouldertime",
                table: "boulder_attempts",
                columns: new[] { "user_id", "boulder_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_boulder_attempts_user_id_completed_completed_at",
                schema: "bouldertime",
                table: "boulder_attempts",
                columns: new[] { "user_id", "completed", "completed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_boulder_follows_boulder_id",
                schema: "bouldertime",
                table: "boulder_follows",
                column: "boulder_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulder_follows_user_id_boulder_id",
                schema: "bouldertime",
                table: "boulder_follows",
                columns: new[] { "user_id", "boulder_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_boulder_ratings_boulder_id",
                schema: "bouldertime",
                table: "boulder_ratings",
                column: "boulder_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulder_ratings_user_id_boulder_id",
                schema: "bouldertime",
                table: "boulder_ratings",
                columns: new[] { "user_id", "boulder_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gym_follows_gym_id",
                schema: "bouldertime",
                table: "gym_follows",
                column: "gym_id");

            migrationBuilder.CreateIndex(
                name: "ix_gym_follows_user_id_gym_id",
                schema: "bouldertime",
                table: "gym_follows",
                columns: new[] { "user_id", "gym_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sector_follows_sector_id",
                schema: "bouldertime",
                table: "sector_follows",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_sector_follows_user_id_sector_id",
                schema: "bouldertime",
                table: "sector_follows",
                columns: new[] { "user_id", "sector_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boulder_attempts",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "boulder_follows",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "boulder_ratings",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "gym_follows",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "sector_follows",
                schema: "bouldertime");
        }
    }
}
