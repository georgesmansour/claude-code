using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvitationPlatform.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClientContactEmailOrPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "client_accounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            // One-time normalisation + clean-up so existing data satisfies the new UNIQUE(phone)
            // index and matches the canonical form the app now stores (a single leading '+' if the
            // original had one, followed by digits only — same as ContactHelper.NormalizePhone):
            //   1) blank/whitespace phones -> NULL;
            //   2) canonicalise every remaining phone (strip spaces, dashes, brackets, …);
            //   3) any phone that reduces to nothing -> NULL;
            //   4) if a phone is now shared by several rows, keep it on the most-recently-updated
            //      one and clear it on the rest. Safe because every pre-existing client still has an
            //      email (email was required until this migration), so it never removes the only login.
            migrationBuilder.Sql(@"
                UPDATE client_accounts SET phone = NULL
                WHERE phone IS NOT NULL AND btrim(phone) = '';

                UPDATE client_accounts SET phone =
                    (CASE WHEN btrim(phone) LIKE '+%' THEN '+' ELSE '' END)
                    || regexp_replace(phone, '\D', '', 'g')
                WHERE phone IS NOT NULL;

                UPDATE client_accounts SET phone = NULL
                WHERE phone IN ('', '+');

                UPDATE client_accounts c SET phone = NULL
                WHERE c.phone IS NOT NULL AND EXISTS (
                    SELECT 1 FROM client_accounts o
                    WHERE o.phone = c.phone AND o.id <> c.id
                      AND (o.updated_at, o.id) > (c.updated_at, c.id)
                );
            ");

            migrationBuilder.CreateIndex(
                name: "IX_client_accounts_phone",
                table: "client_accounts",
                column: "phone",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_client_accounts_contact",
                table: "client_accounts",
                sql: "email IS NOT NULL OR phone IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_client_accounts_phone",
                table: "client_accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_client_accounts_contact",
                table: "client_accounts");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "client_accounts",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
