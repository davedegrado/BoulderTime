using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8Images : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "avatar_path",
                schema: "bouldertime",
                table: "users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cover_image_path",
                schema: "bouldertime",
                table: "gyms",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_path",
                schema: "bouldertime",
                table: "gyms",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "thumbnail_path",
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
                name: "avatar_path",
                schema: "bouldertime",
                table: "users");

            migrationBuilder.DropColumn(
                name: "cover_image_path",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "logo_path",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "thumbnail_path",
                schema: "bouldertime",
                table: "boulders");
        }
    }
}
