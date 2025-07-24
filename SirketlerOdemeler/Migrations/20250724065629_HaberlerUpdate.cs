using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SirketlerOdemeler.Migrations
{
    /// <inheritdoc />
    public partial class HaberlerUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HaberGorsel",
                table: "Haberler",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HaberYapayZekaYorum",
                table: "Haberler",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HaberGorsel",
                table: "Haberler");

            migrationBuilder.DropColumn(
                name: "HaberYapayZekaYorum",
                table: "Haberler");
        }
    }
}
