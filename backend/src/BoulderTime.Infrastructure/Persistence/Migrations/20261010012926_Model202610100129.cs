using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610100129 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "gym_id",
                schema: "bouldertime",
                table: "grade_systems",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.InsertData(
                schema: "bouldertime",
                table: "grade_systems",
                columns: new[] { "id", "created_at", "gym_id", "is_active", "name", "sort_order", "type", "updated_at" },
                values: new object[,]
                {
                    { new Guid("b07e0000-5ca1-4000-8000-000000000000"), new DateTimeOffset(new DateTime(2026, 10, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "V-scale", 1, "VScale", new DateTimeOffset(new DateTime(2026, 10, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b07e0000-f0a1-4000-8000-000000000000"), new DateTimeOffset(new DateTime(2026, 10, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, "Fontainebleau", 0, "Fontainebleau", new DateTimeOffset(new DateTime(2026, 10, 10, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.InsertData(
                schema: "bouldertime",
                table: "grade_values",
                columns: new[] { "id", "color_hex", "grade_system_id", "is_active", "label", "rank" },
                values: new object[,]
                {
                    { new Guid("b07e0000-5ca1-4000-8001-000000000000"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "VB", 0 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000001"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V0", 1 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000002"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V1", 2 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000003"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V2", 3 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000004"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V3", 4 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000005"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V4", 5 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000006"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V5", 6 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000007"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V6", 7 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000008"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V7", 8 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000009"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V8", 9 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000010"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V9", 10 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000011"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V10", 11 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000012"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V11", 12 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000013"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V12", 13 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000014"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V13", 14 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000015"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V14", 15 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000016"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V15", 16 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000017"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V16", 17 },
                    { new Guid("b07e0000-5ca1-4000-8001-000000000018"), null, new Guid("b07e0000-5ca1-4000-8000-000000000000"), true, "V17", 18 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000000"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "3", 0 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000001"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "4", 1 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000002"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "4+", 2 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000003"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "5", 3 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000004"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "5+", 4 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000005"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "6A", 5 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000006"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "6A+", 6 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000007"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "6B", 7 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000008"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "6B+", 8 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000009"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "6C", 9 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000010"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "6C+", 10 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000011"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "7A", 11 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000012"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "7A+", 12 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000013"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "7B", 13 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000014"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "7B+", 14 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000015"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "7C", 15 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000016"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "7C+", 16 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000017"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "8A", 17 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000018"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "8A+", 18 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000019"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "8B", 19 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000020"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "8B+", 20 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000021"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "8C", 21 },
                    { new Guid("b07e0000-f0a1-4000-8001-000000000022"), null, new Guid("b07e0000-f0a1-4000-8000-000000000000"), true, "8C+", 22 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000000"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000001"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000002"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000003"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000004"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000005"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000006"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000007"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000008"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000009"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000010"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000011"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000012"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000013"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000014"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000015"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000016"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000017"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8001-000000000018"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000000"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000001"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000002"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000003"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000004"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000005"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000006"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000007"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000008"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000009"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000010"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000011"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000012"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000013"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000014"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000015"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000016"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000017"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000018"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000019"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000020"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000021"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_values",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8001-000000000022"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_systems",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-5ca1-4000-8000-000000000000"));

            migrationBuilder.DeleteData(
                schema: "bouldertime",
                table: "grade_systems",
                keyColumn: "id",
                keyValue: new Guid("b07e0000-f0a1-4000-8000-000000000000"));

            migrationBuilder.AlterColumn<Guid>(
                name: "gym_id",
                schema: "bouldertime",
                table: "grade_systems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
