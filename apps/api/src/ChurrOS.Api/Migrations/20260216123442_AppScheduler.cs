using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AppScheduler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_schedule",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    application_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    cron_expression = table.Column<string>(type: "text", nullable: false),
                    http_request = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_schedule", x => new { x.account_id, x.application_id, x.name });
                    table.ForeignKey(
                        name: "fk_application_schedule_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_schedule_application_account_id_application_id",
                        columns: x => new { x.account_id, x.application_id },
                        principalSchema: "cs",
                        principalTable: "application",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_schedule_identity_account_id_created_by_id",
                        columns: x => new { x.account_id, x.created_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_schedule_identity_account_id_modified_by_id",
                        columns: x => new { x.account_id, x.modified_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_schedule_account_id_created_by_id",
                schema: "cs",
                table: "application_schedule",
                columns: new[] { "account_id", "created_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_schedule_account_id_modified_by_id",
                schema: "cs",
                table: "application_schedule",
                columns: new[] { "account_id", "modified_by_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_schedule",
                schema: "cs");
        }
    }
}
