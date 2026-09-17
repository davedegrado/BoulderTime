using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BoulderTime.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase5Community : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boulder_betas",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boulder_betas", x => x.id);
                    table.ForeignKey(
                        name: "fk_boulder_betas_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulder_betas_users_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "boulder_videos",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    caption = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_boulder_videos", x => x.id);
                    table.ForeignKey(
                        name: "fk_boulder_videos_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_boulder_videos_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_boulder_videos_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comments", x => x.id);
                    table.ForeignKey(
                        name: "fk_comments_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comments_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "grade_suggestions",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    boulder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_system_id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_value_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grade_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "fk_grade_suggestions_boulders_boulder_id",
                        column: x => x.boulder_id,
                        principalSchema: "bouldertime",
                        principalTable: "boulders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grade_suggestions_grade_systems_grade_system_id",
                        column: x => x.grade_system_id,
                        principalSchema: "bouldertime",
                        principalTable: "grade_systems",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grade_suggestions_grade_values_grade_value_id",
                        column: x => x.grade_value_id,
                        principalSchema: "bouldertime",
                        principalTable: "grade_values",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_grade_suggestions_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                schema: "bouldertime",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reported_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gym_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    resolution_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reports", x => x.id);
                    table.ForeignKey(
                        name: "fk_reports_gyms_gym_id",
                        column: x => x.gym_id,
                        principalSchema: "bouldertime",
                        principalTable: "gyms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reports_users_reported_by_user_id",
                        column: x => x.reported_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_reports_users_reviewed_by_user_id",
                        column: x => x.reviewed_by_user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "comment_likes",
                schema: "bouldertime",
                columns: table => new
                {
                    comment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comment_likes", x => new { x.comment_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_comment_likes_comments_comment_id",
                        column: x => x.comment_id,
                        principalSchema: "bouldertime",
                        principalTable: "comments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_comment_likes_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "bouldertime",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_boulder_betas_boulder_id",
                schema: "bouldertime",
                table: "boulder_betas",
                column: "boulder_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_boulder_betas_uploaded_by_user_id",
                schema: "bouldertime",
                table: "boulder_betas",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulder_videos_boulder_id_status",
                schema: "bouldertime",
                table: "boulder_videos",
                columns: new[] { "boulder_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_boulder_videos_reviewed_by_user_id",
                schema: "bouldertime",
                table: "boulder_videos",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_boulder_videos_status_updated_at",
                schema: "bouldertime",
                table: "boulder_videos",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_boulder_videos_user_id",
                schema: "bouldertime",
                table: "boulder_videos",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_comment_likes_user_id",
                schema: "bouldertime",
                table: "comment_likes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_comments_boulder_id_created_at",
                schema: "bouldertime",
                table: "comments",
                columns: new[] { "boulder_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_user_id",
                schema: "bouldertime",
                table: "comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_suggestions_boulder_id_grade_value_id",
                schema: "bouldertime",
                table: "grade_suggestions",
                columns: new[] { "boulder_id", "grade_value_id" });

            migrationBuilder.CreateIndex(
                name: "ix_grade_suggestions_boulder_id_user_id_grade_system_id",
                schema: "bouldertime",
                table: "grade_suggestions",
                columns: new[] { "boulder_id", "user_id", "grade_system_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grade_suggestions_grade_system_id",
                schema: "bouldertime",
                table: "grade_suggestions",
                column: "grade_system_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_suggestions_grade_value_id",
                schema: "bouldertime",
                table: "grade_suggestions",
                column: "grade_value_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_suggestions_user_id",
                schema: "bouldertime",
                table: "grade_suggestions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_gym_id_status_created_at",
                schema: "bouldertime",
                table: "reports",
                columns: new[] { "gym_id", "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reports_reported_by_user_id_entity_type_entity_id",
                schema: "bouldertime",
                table: "reports",
                columns: new[] { "reported_by_user_id", "entity_type", "entity_id" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ix_reports_reviewed_by_user_id",
                schema: "bouldertime",
                table: "reports",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_reports_status_created_at",
                schema: "bouldertime",
                table: "reports",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boulder_betas",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "boulder_videos",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "comment_likes",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "grade_suggestions",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "reports",
                schema: "bouldertime");

            migrationBuilder.DropTable(
                name: "comments",
                schema: "bouldertime");
        }
    }
}
