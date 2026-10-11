using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610112039 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_unlisted",
                schema: "bouldertime",
                table: "gyms",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_unlisted",
                schema: "bouldertime",
                table: "gyms");
        }
    }
}
