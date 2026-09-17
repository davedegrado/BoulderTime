using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8Locations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "bouldertime",
                table: "gyms",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "bouldertime",
                table: "gyms",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_gyms_latitude_longitude",
                schema: "bouldertime",
                table: "gyms",
                columns: new[] { "latitude", "longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_gyms_latitude_longitude",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "bouldertime",
                table: "gyms");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "bouldertime",
                table: "gyms");
        }
    }
}
