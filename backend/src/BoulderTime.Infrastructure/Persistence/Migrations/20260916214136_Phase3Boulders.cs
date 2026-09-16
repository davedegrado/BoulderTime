using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3Boulders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boulders",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    hold_color = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    setter_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boulders", x => x.id);
                    table.ForeignKey(
                        name: "fk_boulders_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulders_sectors_sector_id",
                        column: x => x.sector_id,
                        principalSchema: "bouldertime",
                        principalTable: "sectors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulders_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_boulders_users_removed_by_user_id",
                        column: x => x.removed_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_boulders_users_setter_user_id",
                        column: x => x.setter_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "grade_systems",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_systems", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_systems_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grade_values",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_system_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    color_hex = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_values", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_values_grade_systems_grade_system_id",
                        column: x => x.grade_system_id,
                        principalSchema: "bouldertime",
                        principalTable: "grade_systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "boulder_grades",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_system_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_value_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boulder_grades", x => x.id);
                    table.ForeignKey(
                        name: "fk_boulder_grades_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulder_grades_grade_systems_grade_system_id",
                        column: x => x.grade_system_id,
                        principalSchema: "bouldertime",
                        principalTable: "grade_systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulder_grades_grade_values_grade_value_id",
                        column: x => x.grade_value_id,
                        principalSchema: "bouldertime",
                        principalTable: "grade_values",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulder_grades_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_boulder_grades_boulder_id_grade_system_id",
                schema: "bouldertime",
                table: "boulder_grades",
                columns: new[] { "boulder_id", "grade_system_id" },
                unique: true,
                filter: "source = 'Staff'");

            migrationBuilder.CreateIndex(
                name: "ix_boulder_grades_created_by_user_id",
                schema: "bouldertime",
                table: "boulder_grades",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulder_grades_grade_system_id",
                schema: "bouldertime",
                table: "boulder_grades",
                column: "grade_system_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulder_grades_grade_value_id",
                schema: "bouldertime",
                table: "boulder_grades",
                column: "grade_value_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulders_created_by_user_id",
                schema: "bouldertime",
                table: "boulders",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulders_gym_id_status_created_at",
                schema: "bouldertime",
                table: "boulders",
                columns: new[] { "gym_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_boulders_removed_by_user_id",
                schema: "bouldertime",
                table: "boulders",
                column: "removed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulders_sector_id_status",
                schema: "bouldertime",
                table: "boulders",
                columns: new[] { "sector_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_boulders_setter_user_id",
                schema: "bouldertime",
                table: "boulders",
                column: "setter_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_systems_gym_id_name",
                schema: "bouldertime",
                table: "grade_systems",
                columns: new[] { "gym_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grade_systems_gym_id_sort_order",
                schema: "bouldertime",
                table: "grade_systems",
                columns: new[] { "gym_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_grade_values_grade_system_id_rank",
                schema: "bouldertime",
                table: "grade_values",
                columns: new[] { "grade_system_id", "rank" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boulder_grades",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "boulders",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "grade_values",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "grade_systems",
                schema: "bouldertime");
        }
    }
}
