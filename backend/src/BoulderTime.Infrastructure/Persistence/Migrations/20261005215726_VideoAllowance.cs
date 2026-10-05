using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VideoAllowance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "community_videos_enabled",
                schema: "bouldertime",
                table: "gyms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "official_beta_limit",
                schema: "bouldertime",
                table: "gyms",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "community_videos_enabled",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "official_beta_limit",
                schema: "bouldertime",
                table: "gyms");
        }
    }
}
