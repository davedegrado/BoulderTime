using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Gyms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gyms",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "character varying(88)", maxLength: 88, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    website = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    cover_image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gyms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gym_candidates",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    official_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    website = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    handled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    handled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gym_candidates", x => x.id);
                    table.ForeignKey(
                        name: "fk_gym_candidates_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_gym_candidates_users_handled_by_user_id",
                        column: x => x.handled_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_gym_candidates_users_submitted_by_user_id",
                        column: x => x.submitted_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "gym_staff",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_gym_staff", x => x.id);
                    table.ForeignKey(
                        name: "fk_gym_staff_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_gym_staff_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_gym_staff_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sectors",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sectors", x => x.id);
                    table.ForeignKey(
                        name: "fk_sectors_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_invitations",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_invitations", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_invitations_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_invitations_users_accepted_by_user_id",
                        column: x => x.accepted_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_staff_invitations_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_gym_candidates_gym_id",
                schema: "bouldertime",
                table: "gym_candidates",
                column: "gym_id");

            migrationBuilder.CreateIndex(
                name: "ix_gym_candidates_handled_by_user_id",
                schema: "bouldertime",
                table: "gym_candidates",
                column: "handled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_gym_candidates_status_created_at",
                schema: "bouldertime",
                table: "gym_candidates",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_gym_candidates_submitted_by_user_id",
                schema: "bouldertime",
                table: "gym_candidates",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_gym_staff_gym_id_user_id",
                schema: "bouldertime",
                table: "gym_staff",
                columns: new[] { "gym_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gym_staff_invited_by_user_id",
                schema: "bouldertime",
                table: "gym_staff",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_gym_staff_user_id",
                schema: "bouldertime",
                table: "gym_staff",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_gyms_slug",
                schema: "bouldertime",
                table: "gyms",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gyms_status_city",
                schema: "bouldertime",
                table: "gyms",
                columns: new[] { "status", "city" });

            migrationBuilder.CreateIndex(
                name: "ix_gyms_status_name",
                schema: "bouldertime",
                table: "gyms",
                columns: new[] { "status", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_sectors_gym_id_name",
                schema: "bouldertime",
                table: "sectors",
                columns: new[] { "gym_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sectors_gym_id_sort_order",
                schema: "bouldertime",
                table: "sectors",
                columns: new[] { "gym_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitations_accepted_by_user_id",
                schema: "bouldertime",
                table: "staff_invitations",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitations_email_status",
                schema: "bouldertime",
                table: "staff_invitations",
                columns: new[] { "email", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitations_gym_id_email",
                schema: "bouldertime",
                table: "staff_invitations",
                columns: new[] { "gym_id", "email" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_staff_invitations_invited_by_user_id",
                schema: "bouldertime",
                table: "staff_invitations",
                column: "invited_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gym_candidates",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "gym_staff",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "sectors",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "staff_invitations",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "gyms",
                schema: "bouldertime");
        }
    }
}
