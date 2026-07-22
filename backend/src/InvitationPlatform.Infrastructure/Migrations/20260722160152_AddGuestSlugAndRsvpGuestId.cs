using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvitationPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestSlugAndRsvpGuestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "guest_id",
                table: "rsvps",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "guests",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_rsvps_guest_id",
                table: "rsvps",
                column: "guest_id");

            migrationBuilder.CreateIndex(
                name: "IX_guests_slug",
                table: "guests",
                column: "slug");

            migrationBuilder.AddForeignKey(
                name: "FK_rsvps_guests_guest_id",
                table: "rsvps",
                column: "guest_id",
                principalTable: "guests",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rsvps_guests_guest_id",
                table: "rsvps");

            migrationBuilder.DropIndex(
                name: "IX_rsvps_guest_id",
                table: "rsvps");

            migrationBuilder.DropIndex(
                name: "IX_guests_slug",
                table: "guests");

            migrationBuilder.DropColumn(
                name: "guest_id",
                table: "rsvps");

            migrationBuilder.DropColumn(
                name: "slug",
                table: "guests");
        }
    }
}
