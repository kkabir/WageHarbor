using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paycrest.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaydayEmailsAndGlAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailPayStubs",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "GlAccountsJson",
                table: "Companies",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailPayStubs",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "GlAccountsJson",
                table: "Companies");
        }
    }
}
