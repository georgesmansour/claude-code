using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvitationPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    max_attendees = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    selected_attendees = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Pending"),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guests", x => x.id);
                    table.ForeignKey(
                        name: "FK_guests_invitations_invitation_id",
                        column: x => x.invitation_id,
                        principalTable: "invitations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guests_invitation_id_name",
                table: "guests",
                columns: new[] { "invitation_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_guests_token",
                table: "guests",
                column: "token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guests");
        }
    }
}
