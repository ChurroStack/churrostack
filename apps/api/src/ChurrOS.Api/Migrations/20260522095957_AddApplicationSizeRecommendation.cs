using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationSizeRecommendation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_size_recommendation",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    application_id = table.Column<long>(type: "bigint", nullable: false),
                    recommended_size = table.Column<string>(type: "jsonb", nullable: true),
                    cpu_avg = table.Column<double>(type: "double precision", nullable: false),
                    cpu_max = table.Column<double>(type: "double precision", nullable: false),
                    cpu_p95 = table.Column<double>(type: "double precision", nullable: false),
                    memory_avg = table.Column<double>(type: "double precision", nullable: false),
                    memory_max = table.Column<double>(type: "double precision", nullable: false),
                    memory_p95 = table.Column<double>(type: "double precision", nullable: false),
                    sample_count = table.Column<int>(type: "integer", nullable: false),
                    window_days = table.Column<int>(type: "integer", nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_size_recommendation", x => new { x.account_id, x.application_id });
                    table.ForeignKey(
                        name: "fk_application_size_recommendation_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_size_recommendation_application_account_id_appl",
                        columns: x => new { x.account_id, x.application_id },
                        principalSchema: "cs",
                        principalTable: "application",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_size_recommendation",
                schema: "cs");
        }
    }
}
