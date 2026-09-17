using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6Notifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gym_announcements",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sector_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    image_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    event_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    notify_followers = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gym_announcements", x => x.id);
                    table.ForeignKey(
                        name: "fk_gym_announcements_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_gym_announcements_sectors_sector_id",
                        column: x => x.sector_id,
                        principalSchema: "bouldertime",
                        principalTable: "sectors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_gym_announcements_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification_settings",
                schema: "bouldertime",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_updates = table.Column<bool>(type: "boolean", nullable: false),
                    sector_updates = table.Column<bool>(type: "boolean", nullable: false),
                    boulder_updates = table.Column<bool>(type: "boolean", nullable: false),
                    my_content = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_notification_settings_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    title = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    related_entity_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: true),
                    link = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    collapse_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    count = table.Column<int>(type: "integer", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_notifications_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gym_announcements_created_by_user_id",
                schema: "bouldertime",
                table: "gym_announcements",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_gym_announcements_gym_id_created_at",
                schema: "bouldertime",
                table: "gym_announcements",
                columns: new[] { "gym_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_gym_announcements_sector_id",
                schema: "bouldertime",
                table: "gym_announcements",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_collapse_key_user_id",
                schema: "bouldertime",
                table: "notifications",
                columns: new[] { "collapse_key", "user_id" },
                filter: "read_at IS NULL AND collapse_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_created_at",
                schema: "bouldertime",
                table: "notifications",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_read_at",
                schema: "bouldertime",
                table: "notifications",
                columns: new[] { "user_id", "read_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gym_announcements",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "notification_settings",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "notifications",
                schema: "bouldertime");
        }
    }
}
