using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SirketlerOdemeler.Migrations
{
    /// <inheritdoc />
    public partial class Haberler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Haberler",
                columns: table => new
                {
                    HaberId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SKod = table.Column<int>(type: "int", nullable: false),
                    HaberBaslik = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HaberIcerik = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Haberler", x => x.HaberId);
                    table.ForeignKey(
                        name: "FK_Haberler_Sirketler_SKod",
                        column: x => x.SKod,
                        principalTable: "Sirketler",
                        principalColumn: "SKod",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Haberler_SKod",
                table: "Haberler",
                column: "SKod");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Haberler");
        }
    }
}
