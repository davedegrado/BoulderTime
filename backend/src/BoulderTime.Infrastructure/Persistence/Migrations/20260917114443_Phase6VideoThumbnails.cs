using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase6VideoThumbnails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "thumbnail_path",
                schema: "bouldertime",
                table: "boulder_videos",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "thumbnail_path",
                schema: "bouldertime",
                table: "boulder_betas",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "thumbnail_path",
                schema: "bouldertime",
                table: "boulder_videos");

            migrationBuilder.DropColumn(
                name: "thumbnail_path",
                schema: "bouldertime",
                table: "boulder_betas");
        }
    }
}
