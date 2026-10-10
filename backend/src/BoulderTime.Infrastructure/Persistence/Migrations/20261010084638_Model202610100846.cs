using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610100846 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "map_zone",
                schema: "bouldertime",
                table: "sectors",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "floor_plan_height",
                schema: "bouldertime",
                table: "gyms",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "floor_plan_path",
                schema: "bouldertime",
                table: "gyms",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "floor_plan_url",
                schema: "bouldertime",
                table: "gyms",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "floor_plan_width",
                schema: "bouldertime",
                table: "gyms",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "map_zone",
                schema: "bouldertime",
                table: "sectors");

            migrationBuilder.DropColumn(
                name: "floor_plan_height",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "floor_plan_path",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "floor_plan_url",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "floor_plan_width",
                schema: "bouldertime",
                table: "gyms");
        }
    }
}
