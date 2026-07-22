using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvitationPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationLabelAndImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "image_url",
                table: "locations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "label",
                table: "locations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "image_url",
                table: "locations");

            migrationBuilder.DropColumn(
                name: "label",
                table: "locations");
        }
    }
}
