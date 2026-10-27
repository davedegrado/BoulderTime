using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610271200 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "climbing_shoes",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    brand = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    key = table.Column<string>(type: "character varying(170)", maxLength: 170, nullable: false),
                    search_text = table.Column<string>(type: "character varying(170)", maxLength: 170, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    product_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_climbing_shoes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_climbing_shoes",
                schema: "bouldertime",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    shoe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_climbing_shoes", x => new { x.user_id, x.shoe_id });
                    table.ForeignKey(
                        name: "fk_user_climbing_shoes_climbing_shoes_shoe_id",
                        column: x => x.shoe_id,
                        principalSchema: "bouldertime",
                        principalTable: "climbing_shoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_climbing_shoes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_climbing_shoes_brand_name",
                schema: "bouldertime",
                table: "climbing_shoes",
                columns: new[] { "brand", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_climbing_shoes_key",
                schema: "bouldertime",
                table: "climbing_shoes",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_climbing_shoes_shoe_id",
                schema: "bouldertime",
                table: "user_climbing_shoes",
                column: "shoe_id");

            migrationBuilder.CreateIndex(
                name: "ux_user_climbing_shoes_one_primary",
                schema: "bouldertime",
                table: "user_climbing_shoes",
                column: "user_id",
                unique: true,
                filter: "is_primary");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_climbing_shoes",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "climbing_shoes",
                schema: "bouldertime");
        }
    }
}
