using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Model202610081222 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "suspended_emails",
                schema: "bouldertime",
                columns: table => new
                {
                    email_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suspended_emails", x => x.email_hash);
                });

            migrationBuilder.CreateIndex(
                name: "ix_suspended_emails_recorded_at",
                schema: "bouldertime",
                table: "suspended_emails",
                column: "recorded_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "suspended_emails",
                schema: "bouldertime");
        }
    }
}
