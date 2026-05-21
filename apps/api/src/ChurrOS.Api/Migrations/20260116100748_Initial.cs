using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ChurrOS.Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "cs");

            migrationBuilder.EnsureSchema(
                name: "auth");

            migrationBuilder.CreateTable(
                name: "account",
                schema: "cs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    domains = table.Column<string[]>(type: "text[]", nullable: false),
                    owners = table.Column<string[]>(type: "text[]", nullable: false),
                    encryption_key = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_account", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "application",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_type = table.Column<string>(type: "text", nullable: true),
                    client_id = table.Column<string>(type: "text", nullable: true),
                    client_secret = table.Column<string>(type: "text", nullable: true),
                    client_type = table.Column<string>(type: "text", nullable: true),
                    concurrency_token = table.Column<string>(type: "text", nullable: true),
                    consent_type = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    display_names = table.Column<string>(type: "text", nullable: true),
                    json_web_key_set = table.Column<string>(type: "text", nullable: true),
                    permissions = table.Column<string>(type: "text", nullable: true),
                    post_logout_redirect_uris = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    redirect_uris = table.Column<string>(type: "text", nullable: true),
                    requirements = table.Column<string>(type: "text", nullable: true),
                    settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application1", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "application_event",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    application_id = table.Column<long>(type: "bigint", nullable: false),
                    environment_id = table.Column<long>(type: "bigint", nullable: false),
                    deployment_name = table.Column<string>(type: "text", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    target = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "application_trace",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    environment_id = table.Column<long>(type: "bigint", nullable: false),
                    application_id = table.Column<long>(type: "bigint", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    identity_id = table.Column<long>(type: "bigint", nullable: true),
                    method = table.Column<string>(type: "text", nullable: true),
                    protocol = table.Column<string>(type: "text", nullable: true),
                    service = table.Column<string>(type: "text", nullable: true),
                    host = table.Column<string>(type: "text", nullable: true),
                    path = table.Column<string>(type: "text", nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    is_error = table.Column<bool>(type: "boolean", nullable: false),
                    client_ip = table.Column<string>(type: "text", nullable: true),
                    request_bytes = table.Column<long>(type: "bigint", nullable: false),
                    response_bytes = table.Column<long>(type: "bigint", nullable: false),
                    duration = table.Column<long>(type: "bigint", nullable: false),
                    tags = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "metric_value",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    metric_id = table.Column<long>(type: "bigint", nullable: false),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    value = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "role",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_claim",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claim", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scope",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    concurrency_token = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    descriptions = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    display_names = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scope", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_login",
                schema: "auth",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_login", x => new { x.login_provider, x.provider_key });
                });

            migrationBuilder.CreateTable(
                name: "user_passkey",
                schema: "auth",
                columns: table => new
                {
                    credential_id = table.Column<byte[]>(type: "bytea", maxLength: 1024, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_passkey", x => x.credential_id);
                });

            migrationBuilder.CreateTable(
                name: "user_token",
                schema: "auth",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_token", x => new { x.user_id, x.login_provider, x.name });
                });

            migrationBuilder.CreateTable(
                name: "acl",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acl", x => new { x.account_id, x.id });
                    table.ForeignKey(
                        name: "fk_acl_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "metric",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    metric_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<byte>(type: "smallint", nullable: false),
                    labels = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_metric", x => new { x.account_id, x.hash });
                    table.ForeignKey(
                        name: "fk_metric_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "template_category",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    title = table.Column<string>(type: "character varying(4095)", maxLength: 4095, nullable: false),
                    icon = table.Column<string>(type: "character varying(4095)", maxLength: 4095, nullable: false),
                    translation = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_category", x => new { x.account_id, x.id });
                    table.ForeignKey(
                        name: "fk_template_category_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "authorization",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    concurrency_token = table.Column<string>(type: "text", nullable: true),
                    creation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    scopes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true),
                    subject = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_authorization", x => x.id);
                    table.ForeignKey(
                        name: "fk_authorization_application_application_id",
                        column: x => x.application_id,
                        principalSchema: "auth",
                        principalTable: "application",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_role",
                schema: "auth",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_role_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "auth",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claim",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claim", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claim_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "auth",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<byte>(type: "smallint", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    properties = table.Column<string>(type: "jsonb", nullable: false),
                    acl_id = table.Column<long>(type: "bigint", nullable: true),
                    lock_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity", x => new { x.account_id, x.id });
                    table.ForeignKey(
                        name: "fk_identity_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_identity_acl_account_id_acl_id",
                        columns: x => new { x.account_id, x.acl_id },
                        principalSchema: "cs",
                        principalTable: "acl",
                        principalColumns: new[] { "account_id", "id" });
                    table.ForeignKey(
                        name: "fk_identity_identity_account_id_created_by_id",
                        columns: x => new { x.account_id, x.created_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" });
                    table.ForeignKey(
                        name: "fk_identity_identity_account_id_modified_by_id",
                        columns: x => new { x.account_id, x.modified_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" });
                });

            migrationBuilder.CreateTable(
                name: "token",
                schema: "auth",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: true),
                    authorization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    concurrency_token = table.Column<string>(type: "text", nullable: true),
                    creation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payload = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    redemption_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reference_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: true),
                    subject = table.Column<string>(type: "text", nullable: true),
                    type = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_token_application_application_id",
                        column: x => x.application_id,
                        principalSchema: "auth",
                        principalTable: "application",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_token_authorization_authorization_id",
                        column: x => x.authorization_id,
                        principalSchema: "auth",
                        principalTable: "authorization",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "acl_member",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    acl_id = table.Column<long>(type: "bigint", nullable: false),
                    identity_id = table.Column<long>(type: "bigint", nullable: false),
                    permission = table.Column<byte>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_acl_member", x => new { x.account_id, x.acl_id, x.identity_id });
                    table.ForeignKey(
                        name: "fk_acl_member_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_acl_member_acl_account_id_acl_id",
                        columns: x => new { x.account_id, x.acl_id },
                        principalSchema: "cs",
                        principalTable: "acl",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_acl_member_identity_account_id_identity_id",
                        columns: x => new { x.account_id, x.identity_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "api_key",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<byte[]>(type: "bytea", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    identity_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_key", x => new { x.account_id, x.id });
                    table.ForeignKey(
                        name: "fk_api_key_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_api_key_identity_account_id_created_by_id",
                        columns: x => new { x.account_id, x.created_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_api_key_identity_account_id_identity_id",
                        columns: x => new { x.account_id, x.identity_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_api_key_identity_account_id_modified_by_id",
                        columns: x => new { x.account_id, x.modified_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "environment",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    host = table.Column<string[]>(type: "text[]", maxLength: 4095, nullable: false),
                    port = table.Column<int>(type: "integer", nullable: false),
                    ssh_public_key = table.Column<byte[]>(type: "bytea", nullable: false),
                    encryption_key = table.Column<string>(type: "text", nullable: false),
                    acl_id = table.Column<long>(type: "bigint", nullable: false),
                    acl_account_id = table.Column<long>(type: "bigint", nullable: true),
                    provision_status = table.Column<int>(type: "integer", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: true),
                    health = table.Column<string>(type: "jsonb", nullable: true),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_environment", x => new { x.account_id, x.id });
                    table.ForeignKey(
                        name: "fk_environment_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_environment_acl_acl_account_id_acl_id",
                        columns: x => new { x.acl_account_id, x.acl_id },
                        principalSchema: "cs",
                        principalTable: "acl",
                        principalColumns: new[] { "account_id", "id" });
                    table.ForeignKey(
                        name: "fk_environment_identity_account_id_created_by_id",
                        columns: x => new { x.account_id, x.created_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_environment_identity_account_id_modified_by_id",
                        columns: x => new { x.account_id, x.modified_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identity_member",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    identity_id = table.Column<long>(type: "bigint", nullable: false),
                    group_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_identity_member", x => new { x.account_id, x.identity_id, x.group_id });
                    table.ForeignKey(
                        name: "fk_identity_member_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_identity_member_identity_account_id_group_id",
                        columns: x => new { x.account_id, x.group_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_identity_member_identity_account_id_identity_id",
                        columns: x => new { x.account_id, x.identity_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "llm",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false),
                    names = table.Column<string[]>(type: "text[]", nullable: false),
                    acl_id = table.Column<long>(type: "bigint", nullable: false),
                    routing = table.Column<byte>(type: "smallint", nullable: false),
                    destination = table.Column<string>(type: "jsonb", nullable: false),
                    fallback = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by_id = table.Column<long>(type: "bigint", nullable: false),
                    capabilities = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_llm", x => new { x.account_id, x.id });
                    table.ForeignKey(
                        name: "fk_llm_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_llm_acl_account_id_acl_id",
                        columns: x => new { x.account_id, x.acl_id },
                        principalSchema: "cs",
                        principalTable: "acl",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_llm_identity_account_id_created_by_id",
                        columns: x => new { x.account_id, x.created_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_llm_identity_account_id_modified_by_id",
                        columns: x => new { x.account_id, x.modified_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "template",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true, computedColumnSql: "(definition ->> 'name')", stored: true),
                    target = table.Column<string>(type: "text", nullable: true, computedColumnSql: "(definition ->> 'target')", stored: true),
                    title = table.Column<string>(type: "text", nullable: true, computedColumnSql: "(definition ->> 'title')", stored: true),
                    description = table.Column<string>(type: "text", nullable: true, computedColumnSql: "(definition ->> 'description')", stored: true),
                    icon = table.Column<string>(type: "text", nullable: true, computedColumnSql: "(definition ->> 'icon')", stored: true),
                    type = table.Column<string>(type: "text", nullable: true, computedColumnSql: "(definition ->> 'type')", stored: true),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template", x => new { x.account_id, x.id });
                    table.ForeignKey(
                        name: "fk_template_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_template_identity_account_id_created_by_id",
                        columns: x => new { x.account_id, x.created_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_template_identity_account_id_modified_by_id",
                        columns: x => new { x.account_id, x.modified_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_template_template_category_account_id_category_id",
                        columns: x => new { x.account_id, x.category_id },
                        principalSchema: "cs",
                        principalTable: "template_category",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    id = table.Column<long>(type: "bigint", nullable: false),
                    environment_id = table.Column<long>(type: "bigint", nullable: false),
                    acl_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    template_id = table.Column<long>(type: "bigint", nullable: false),
                    mode = table.Column<int>(type: "integer", nullable: false),
                    size = table.Column<string>(type: "jsonb", nullable: false),
                    replicas = table.Column<int>(type: "integer", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    variables = table.Column<string>(type: "jsonb", nullable: false),
                    ports = table.Column<string>(type: "jsonb", nullable: true),
                    deployment_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application", x => new { x.account_id, x.id });
                    table.ForeignKey(
                        name: "fk_application_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_acl_account_id_acl_id",
                        columns: x => new { x.account_id, x.acl_id },
                        principalSchema: "cs",
                        principalTable: "acl",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_environment_account_id_environment_id",
                        columns: x => new { x.account_id, x.environment_id },
                        principalSchema: "cs",
                        principalTable: "environment",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_identity_account_id_created_by_id",
                        columns: x => new { x.account_id, x.created_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_identity_account_id_modified_by_id",
                        columns: x => new { x.account_id, x.modified_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_template_account_id_template_id",
                        columns: x => new { x.account_id, x.template_id },
                        principalSchema: "cs",
                        principalTable: "template",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_deployment",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    application_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    deployment_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    owner_id = table.Column<long>(type: "bigint", nullable: true),
                    owner_account_id = table.Column<long>(type: "bigint", nullable: true),
                    provision_status = table.Column<int>(type: "integer", nullable: false),
                    execution_status = table.Column<int>(type: "integer", nullable: false),
                    deployment_status = table.Column<string>(type: "jsonb", nullable: true),
                    deployed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_deployment", x => new { x.account_id, x.application_id, x.name });
                    table.ForeignKey(
                        name: "fk_application_deployment_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_deployment_application_account_id_application_id",
                        columns: x => new { x.account_id, x.application_id },
                        principalSchema: "cs",
                        principalTable: "application",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_deployment_identity_account_id_created_by_id",
                        columns: x => new { x.account_id, x.created_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_deployment_identity_account_id_modified_by_id",
                        columns: x => new { x.account_id, x.modified_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_deployment_identity_owner_account_id_owner_id",
                        columns: x => new { x.owner_account_id, x.owner_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" });
                });

            migrationBuilder.CreateTable(
                name: "application_extension",
                schema: "cs",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    application_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    environment_id = table.Column<long>(type: "bigint", nullable: false),
                    environment_account_id = table.Column<long>(type: "bigint", nullable: true),
                    template_id = table.Column<long>(type: "bigint", nullable: false),
                    template_account_id = table.Column<long>(type: "bigint", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    parameters = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: false),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    modified_by_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_extension", x => new { x.account_id, x.application_id, x.name });
                    table.ForeignKey(
                        name: "fk_application_extension_account_account_id",
                        column: x => x.account_id,
                        principalSchema: "cs",
                        principalTable: "account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_extension_application_account_id_application_id",
                        columns: x => new { x.account_id, x.application_id },
                        principalSchema: "cs",
                        principalTable: "application",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_extension_environment_environment_account_id_en",
                        columns: x => new { x.environment_account_id, x.environment_id },
                        principalSchema: "cs",
                        principalTable: "environment",
                        principalColumns: new[] { "account_id", "id" });
                    table.ForeignKey(
                        name: "fk_application_extension_identity_account_id_created_by_id",
                        columns: x => new { x.account_id, x.created_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_extension_identity_account_id_modified_by_id",
                        columns: x => new { x.account_id, x.modified_by_id },
                        principalSchema: "cs",
                        principalTable: "identity",
                        principalColumns: new[] { "account_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_application_extension_template_template_account_id_template",
                        columns: x => new { x.template_account_id, x.template_id },
                        principalSchema: "cs",
                        principalTable: "template",
                        principalColumns: new[] { "account_id", "id" });
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_domains",
                schema: "cs",
                table: "account",
                column: "domains")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_acl_member_account_id_identity_id",
                schema: "cs",
                table: "acl_member",
                columns: new[] { "account_id", "identity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_api_key_account_id_created_by_id",
                schema: "cs",
                table: "api_key",
                columns: new[] { "account_id", "created_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_api_key_account_id_identity_id",
                schema: "cs",
                table: "api_key",
                columns: new[] { "account_id", "identity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_api_key_account_id_modified_by_id",
                schema: "cs",
                table: "api_key",
                columns: new[] { "account_id", "modified_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_api_key_account_id_value",
                schema: "cs",
                table: "api_key",
                columns: new[] { "account_id", "value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_account_id_acl_id",
                schema: "cs",
                table: "application",
                columns: new[] { "account_id", "acl_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_account_id_created_by_id",
                schema: "cs",
                table: "application",
                columns: new[] { "account_id", "created_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_account_id_environment_id",
                schema: "cs",
                table: "application",
                columns: new[] { "account_id", "environment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_account_id_modified_by_id",
                schema: "cs",
                table: "application",
                columns: new[] { "account_id", "modified_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_account_id_name",
                schema: "cs",
                table: "application",
                columns: new[] { "account_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_account_id_template_id",
                schema: "cs",
                table: "application",
                columns: new[] { "account_id", "template_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_tags",
                schema: "cs",
                table: "application",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_application_deployment_account_id_created_by_id",
                schema: "cs",
                table: "application_deployment",
                columns: new[] { "account_id", "created_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_deployment_account_id_modified_by_id",
                schema: "cs",
                table: "application_deployment",
                columns: new[] { "account_id", "modified_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_deployment_account_id_name",
                schema: "cs",
                table: "application_deployment",
                columns: new[] { "account_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_deployment_owner_account_id_owner_id",
                schema: "cs",
                table: "application_deployment",
                columns: new[] { "owner_account_id", "owner_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_event_account_id_application_id_target",
                schema: "cs",
                table: "application_event",
                columns: new[] { "account_id", "application_id", "target" });

            migrationBuilder.CreateIndex(
                name: "ix_application_event_account_id_environment_id_target",
                schema: "cs",
                table: "application_event",
                columns: new[] { "account_id", "environment_id", "target" });

            migrationBuilder.CreateIndex(
                name: "ix_application_extension_account_id_created_by_id",
                schema: "cs",
                table: "application_extension",
                columns: new[] { "account_id", "created_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_extension_account_id_modified_by_id",
                schema: "cs",
                table: "application_extension",
                columns: new[] { "account_id", "modified_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_extension_environment_account_id_environment_id",
                schema: "cs",
                table: "application_extension",
                columns: new[] { "environment_account_id", "environment_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_extension_template_account_id_template_id",
                schema: "cs",
                table: "application_extension",
                columns: new[] { "template_account_id", "template_id" });

            migrationBuilder.CreateIndex(
                name: "ix_application_trace_account_id_application_id_service",
                schema: "cs",
                table: "application_trace",
                columns: new[] { "account_id", "application_id", "service" });

            migrationBuilder.CreateIndex(
                name: "ix_application_trace_account_id_environment_id_service",
                schema: "cs",
                table: "application_trace",
                columns: new[] { "account_id", "environment_id", "service" });

            migrationBuilder.CreateIndex(
                name: "ix_authorization_application_id",
                schema: "auth",
                table: "authorization",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_environment_account_id_created_by_id",
                schema: "cs",
                table: "environment",
                columns: new[] { "account_id", "created_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_environment_account_id_modified_by_id",
                schema: "cs",
                table: "environment",
                columns: new[] { "account_id", "modified_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_environment_account_id_name",
                schema: "cs",
                table: "environment",
                columns: new[] { "account_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_environment_acl_account_id_acl_id",
                schema: "cs",
                table: "environment",
                columns: new[] { "acl_account_id", "acl_id" });

            migrationBuilder.CreateIndex(
                name: "ix_environment_ssh_public_key",
                schema: "cs",
                table: "environment",
                column: "ssh_public_key");

            migrationBuilder.CreateIndex(
                name: "ix_environment_tags",
                schema: "cs",
                table: "environment",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_identity_account_id_acl_id",
                schema: "cs",
                table: "identity",
                columns: new[] { "account_id", "acl_id" });

            migrationBuilder.CreateIndex(
                name: "ix_identity_account_id_created_by_id",
                schema: "cs",
                table: "identity",
                columns: new[] { "account_id", "created_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_identity_account_id_modified_by_id",
                schema: "cs",
                table: "identity",
                columns: new[] { "account_id", "modified_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_identity_account_id_name",
                schema: "cs",
                table: "identity",
                columns: new[] { "account_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_identity_member_account_id_group_id",
                schema: "cs",
                table: "identity_member",
                columns: new[] { "account_id", "group_id" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_account_id_acl_id",
                schema: "cs",
                table: "llm",
                columns: new[] { "account_id", "acl_id" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_account_id_created_by_id",
                schema: "cs",
                table: "llm",
                columns: new[] { "account_id", "created_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_account_id_modified_by_id",
                schema: "cs",
                table: "llm",
                columns: new[] { "account_id", "modified_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_llm_account_id_names",
                schema: "cs",
                table: "llm",
                columns: new[] { "account_id", "names" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_metric_value_account_id_metric_id",
                schema: "cs",
                table: "metric_value",
                columns: new[] { "account_id", "metric_id" });

            migrationBuilder.CreateIndex(
                name: "role_name_index",
                schema: "auth",
                table: "role",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_template_account_id_category_id",
                schema: "cs",
                table: "template",
                columns: new[] { "account_id", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_template_account_id_created_by_id",
                schema: "cs",
                table: "template",
                columns: new[] { "account_id", "created_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_template_account_id_modified_by_id",
                schema: "cs",
                table: "template",
                columns: new[] { "account_id", "modified_by_id" });

            migrationBuilder.CreateIndex(
                name: "ix_template_account_id_name_target",
                schema: "cs",
                table: "template",
                columns: new[] { "account_id", "name", "target" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_template_category_account_id_name",
                schema: "cs",
                table: "template_category",
                columns: new[] { "account_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_token_application_id",
                schema: "auth",
                table: "token",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_token_authorization_id",
                schema: "auth",
                table: "token",
                column: "authorization_id");

            migrationBuilder.CreateIndex(
                name: "email_index",
                schema: "auth",
                table: "user",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "username_index",
                schema: "auth",
                table: "user",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_claim_user_id",
                schema: "auth",
                table: "user_claim",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_role_id",
                schema: "auth",
                table: "user_role",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "acl_member",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "api_key",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "application_deployment",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "application_event",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "application_extension",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "application_trace",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "identity_member",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "llm",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "metric",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "metric_value",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "role_claim",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "scope",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "token",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "user_claim",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "user_login",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "user_passkey",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "user_role",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "user_token",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "application",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "authorization",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "user",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "role",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "environment",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "template",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "application",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "identity",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "template_category",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "acl",
                schema: "cs");

            migrationBuilder.DropTable(
                name: "account",
                schema: "cs");
        }
    }
}
