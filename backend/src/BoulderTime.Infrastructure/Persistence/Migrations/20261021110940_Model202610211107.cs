using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610211107 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "photo_recompressions",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    old_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    new_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    old_bytes = table.Column<long>(type: "bigint", nullable: false),
                    new_bytes = table.Column<long>(type: "bigint", nullable: false),
                    by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rolled_back_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_photo_recompressions", x => x.id);
                    table.ForeignKey(
                        name: "fk_photo_recompressions_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_photo_recompressions_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_photo_recompressions_users_by_user_id",
                        column: x => x.by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photo_recompressions_boulder_id",
                schema: "bouldertime",
                table: "photo_recompressions",
                column: "boulder_id");

            migrationBuilder.CreateIndex(
                name: "ix_photo_recompressions_by_user_id",
                schema: "bouldertime",
                table: "photo_recompressions",
                column: "by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_photo_recompressions_gym_id_created_at",
                schema: "bouldertime",
                table: "photo_recompressions",
                columns: new[] { "gym_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photo_recompressions",
                schema: "bouldertime");
        }
    }
}
