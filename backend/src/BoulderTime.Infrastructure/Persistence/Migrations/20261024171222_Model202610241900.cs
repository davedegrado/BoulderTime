using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610241900 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "app_version",
                schema: "bouldertime",
                table: "push_subscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "app_version",
                schema: "bouldertime",
                table: "push_subscriptions");
        }
    }
}
