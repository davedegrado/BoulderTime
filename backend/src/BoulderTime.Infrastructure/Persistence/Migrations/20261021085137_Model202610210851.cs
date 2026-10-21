using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610210851 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "highlight_mask_path",
                schema: "bouldertime",
                table: "boulders",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "highlight_settings",
                schema: "bouldertime",
                table: "boulders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "highlight_source_path",
                schema: "bouldertime",
                table: "boulders",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "highlight_mask_path",
                schema: "bouldertime",
                table: "boulders");

            migrationBuilder.DropColumn(
                name: "highlight_settings",
                schema: "bouldertime",
                table: "boulders");

            migrationBuilder.DropColumn(
                name: "highlight_source_path",
                schema: "bouldertime",
                table: "boulders");
        }
    }
}
