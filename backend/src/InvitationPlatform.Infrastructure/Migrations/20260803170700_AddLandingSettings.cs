using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvitationPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLandingSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "landing_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    company_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    whatsapp_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    company_address = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    instagram_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    facebook_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    tiktok_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    pinterest_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    map_embed_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_landing_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "landing_settings");
        }
    }
}
