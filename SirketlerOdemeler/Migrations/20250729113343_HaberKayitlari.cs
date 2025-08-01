using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SirketlerOdemeler.Migrations
{
    /// <inheritdoc />
    public partial class HaberKayitlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HaberKayitlar",
                columns: table => new
                {
                    HaberKayitId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HaberKategori = table.Column<int>(type: "int", nullable: false),
                    KategoriId = table.Column<int>(type: "int", nullable: false),
                    HaberBaslik = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HaberIcerik = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HaberTarih = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HaberYZ1Yorum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HaberYZ2Yorum = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HaberYZ3Yorum = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HaberKayitlar", x => x.HaberKayitId);
                    table.ForeignKey(
                        name: "FK_HaberKayitlar_HaberlerKategoriler_KategoriId",
                        column: x => x.KategoriId,
                        principalTable: "HaberlerKategoriler",
                        principalColumn: "KategoriId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HaberKayitlar_KategoriId",
                table: "HaberKayitlar",
                column: "KategoriId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HaberKayitlar");
        }
    }
}
