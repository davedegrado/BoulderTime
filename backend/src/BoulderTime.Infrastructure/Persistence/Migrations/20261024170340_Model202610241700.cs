using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610241700 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "bio",
                schema: "bouldertime",
                table: "users",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "friend_requests",
                schema: "bouldertime",
                table: "notification_settings",
                type: "boolean",
                nullable: false,
                // Existing settings keep every notification on, friend requests included, as a missing row does.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bio",
                schema: "bouldertime",
                table: "users");

            migrationBuilder.DropColumn(
                name: "friend_requests",
                schema: "bouldertime",
                table: "notification_settings");
        }
    }
}
