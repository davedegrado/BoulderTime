using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610200917 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "beta_test_at",
                schema: "bouldertime",
                table: "boulders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "beta_test_at",
                schema: "bouldertime",
                table: "boulders");
        }
    }
}
