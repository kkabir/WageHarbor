using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paycrest.Data.Migrations
{
    /// <inheritdoc />
    public partial class VisitorLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PageVisits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Referrer = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    VisitorHash = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    Device = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Browser = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    SignedIn = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PageVisits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PageVisits_TimeUtc",
                table: "PageVisits",
                column: "TimeUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PageVisits");
        }
    }
}
