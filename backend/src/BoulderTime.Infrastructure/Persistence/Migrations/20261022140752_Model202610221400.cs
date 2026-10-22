using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610221400 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_version_policies",
                schema: "bouldertime",
                columns: table => new
                {
                    platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    minimum_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recommended_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    store_url = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_version_policies", x => x.platform);
                    table.ForeignKey(
                        name: "fk_app_version_policies_users_updated_by_user_id",
                        column: x => x.updated_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                schema: "bouldertime",
                table: "app_version_policies",
                columns: new[] { "platform", "minimum_version", "recommended_version", "store_url", "updated_at", "updated_by_user_id" },
                values: new object[,]
                {
                    { "Android", "1.0.0", null, "https://play.google.com/store/apps/details?id=com.bouldertime.app", new DateTimeOffset(new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null },
                    { "Ios", "1.0.0", null, null, new DateTimeOffset(new DateTime(2026, 10, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null }
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_version_policies_updated_by_user_id",
                schema: "bouldertime",
                table: "app_version_policies",
                column: "updated_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_version_policies",
                schema: "bouldertime");
        }
    }
}
