using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InvitationPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_builtin = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    data = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_templates_admin_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    slug = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "Draft"),
                    public_token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    event_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    event_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    max_attendees = table.Column<int>(type: "integer", nullable: false, defaultValue: 10),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitations_admin_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_invitations_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_log_admin_accounts_admin_id",
                        column: x => x.admin_id,
                        principalTable: "admin_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_audit_log_invitations_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "client_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    must_change_password = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    last_login_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_client_accounts_admin_accounts_created_by",
                        column: x => x.created_by,
                        principalTable: "admin_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_client_accounts_invitations_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invitation_sections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false, defaultValue: "Cover"),
                    enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_sections", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitation_sections_invitations_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rsvps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response = table.Column<string>(type: "text", nullable: false, defaultValue: "Yes"),
                    party_size = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    contact_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    message = table.Column<string>(type: "text", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rsvps", x => x.id);
                    table.ForeignKey(
                        name: "FK_rsvps_invitations_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_on_rsvp = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sms_on_rsvp = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    daily_summary = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_settings", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_settings_client_accounts_client_id",
                        column: x => x.client_id,
                        principalTable: "client_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gift_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    bank_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    account_number = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    account_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gift_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_gift_accounts_invitation_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "invitation_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    time_label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    map_url = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.id);
                    table.ForeignKey(
                        name: "FK_locations_invitation_sections_section_id",
                        column: x => x.section_id,
                        principalTable: "invitation_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rsvp_guests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    rsvp_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    full_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    age_group = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    meal_preference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    dietary_restrictions = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rsvp_guests", x => x.id);
                    table.ForeignKey(
                        name: "FK_rsvp_guests_rsvps_rsvp_id",
                        column: x => x.rsvp_id,
                        principalTable: "rsvps",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_accounts_email",
                table: "admin_accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_admin_id_created_at",
                table: "audit_log",
                columns: new[] { "admin_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_entity_type_entity_id",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_invitation_id",
                table: "audit_log",
                column: "invitation_id");

            migrationBuilder.CreateIndex(
                name: "IX_client_accounts_created_by",
                table: "client_accounts",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_client_accounts_email",
                table: "client_accounts",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_client_accounts_invitation_id",
                table: "client_accounts",
                column: "invitation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gift_accounts_section_id_order_index",
                table: "gift_accounts",
                columns: new[] { "section_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "IX_invitation_sections_invitation_id_order_index",
                table: "invitation_sections",
                columns: new[] { "invitation_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "IX_invitations_created_by",
                table: "invitations",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_event_date",
                table: "invitations",
                column: "event_date");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_public_token",
                table: "invitations",
                column: "public_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitations_slug",
                table: "invitations",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invitations_status",
                table: "invitations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_invitations_template_id",
                table: "invitations",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_locations_section_id_order_index",
                table: "locations",
                columns: new[] { "section_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_settings_client_id",
                table: "notification_settings",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_rsvp_guests_rsvp_id_order_index",
                table: "rsvp_guests",
                columns: new[] { "rsvp_id", "order_index" });

            migrationBuilder.CreateIndex(
                name: "IX_rsvps_created_at",
                table: "rsvps",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_rsvps_invitation_id",
                table: "rsvps",
                column: "invitation_id");

            migrationBuilder.CreateIndex(
                name: "IX_templates_created_by",
                table: "templates",
                column: "created_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "gift_accounts");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropTable(
                name: "rsvp_guests");

            migrationBuilder.DropTable(
                name: "invitation_sections");

            migrationBuilder.DropTable(
                name: "client_accounts");

            migrationBuilder.DropTable(
                name: "rsvps");

            migrationBuilder.DropTable(
                name: "invitations");

            migrationBuilder.DropTable(
                name: "templates");

            migrationBuilder.DropTable(
                name: "admin_accounts");
        }
    }
}
