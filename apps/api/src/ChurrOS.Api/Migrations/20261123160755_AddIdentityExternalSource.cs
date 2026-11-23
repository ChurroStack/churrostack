using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChurrOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityExternalSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "source",
                schema: "cs",
                table: "identity_member",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "external_id",
                schema: "cs",
                table: "identity",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "source",
                schema: "cs",
                table: "identity",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.CreateIndex(
                name: "ix_identity_account_id_external_id",
                schema: "cs",
                table: "identity",
                columns: new[] { "account_id", "external_id" },
                unique: true,
                filter: "external_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_identity_account_id_external_id",
                schema: "cs",
                table: "identity");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "cs",
                table: "identity_member");

            migrationBuilder.DropColumn(
                name: "external_id",
                schema: "cs",
                table: "identity");

            migrationBuilder.DropColumn(
                name: "source",
                schema: "cs",
                table: "identity");
        }
    }
}
