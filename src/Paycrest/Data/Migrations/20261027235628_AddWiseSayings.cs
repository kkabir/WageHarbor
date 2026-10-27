using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Paycrest.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWiseSayings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WiseSayings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Text = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WiseSayings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WiseSayings");
        }
    }
}
