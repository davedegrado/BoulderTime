using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610120828 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "opening_hours",
                schema: "bouldertime",
                table: "gyms",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color_hex2",
                schema: "bouldertime",
                table: "grade_values",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "points_override",
                schema: "bouldertime",
                table: "grade_values",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retired_points",
                schema: "bouldertime",
                table: "grade_values",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "name",
                schema: "bouldertime",
                table: "boulders",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "points_override",
                schema: "bouldertime",
                table: "boulders",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "announcement_attendances",
                schema: "bouldertime",
                columns: table => new
                {
                    announcement_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcement_attendances", x => new { x.announcement_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_announcement_attendances_gym_announcements_announcement_id",
                        column: x => x.announcement_id,
                        principalSchema: "bouldertime",
                        principalTable: "gym_announcements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_announcement_attendances_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000000"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000001"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000002"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000003"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000004"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000005"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000006"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000007"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000008"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000009"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000010"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000011"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000012"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000013"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000014"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000015"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000016"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000017"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000018"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000000"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000001"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000002"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000003"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000004"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000005"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000006"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000007"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000008"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000009"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000010"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000011"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000012"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000013"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000014"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000015"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000016"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000017"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000018"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000019"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000020"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000021"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.UpdateData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000022"),
                columns: new[] { "color_hex2", "points_override", "retired_points" },
                values: new object[] { null, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_announcement_attendances_user_id",
                schema: "bouldertime",
                table: "announcement_attendances",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcement_attendances",
                schema: "bouldertime");

            migrationBuilder.DropColumn(
                name: "opening_hours",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "color_hex2",
                schema: "bouldertime",
                table: "grade_values");

            migrationBuilder.DropColumn(
                name: "points_override",
                schema: "bouldertime",
                table: "grade_values");

            migrationBuilder.DropColumn(
                name: "retired_points",
                schema: "bouldertime",
                table: "grade_values");

            migrationBuilder.DropColumn(
                name: "name",
                schema: "bouldertime",
                table: "boulders");

            migrationBuilder.DropColumn(
                name: "points_override",
                schema: "bouldertime",
                table: "boulders");
        }
    }
}
