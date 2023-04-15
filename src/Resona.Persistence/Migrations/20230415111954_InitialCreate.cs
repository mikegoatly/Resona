using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Resona.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Album",
                columns: table => new
                {
                    AlbumId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Path = table.Column<string>(type: "TEXT", maxLength: 250, nullable: false),
                    Artist = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ThumbnailFile = table.Column<string>(type: "TEXT", maxLength: 350, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Album", x => x.AlbumId);
                });

            migrationBuilder.CreateTable(
                name: "Track",
                columns: table => new
                {
                    TrackId = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Artist = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    TrackNumber = table.Column<uint>(type: "INTEGER", nullable: false),
                    LastModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AlbumId = table.Column<int>(type: "INTEGER", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Track", x => x.TrackId);
                    table.ForeignKey(
                        name: "FK_Track_Album_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Album",
                        principalColumn: "AlbumId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Album_Kind_Name",
                table: "Album",
                columns: new[] { "Kind", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Track_AlbumId",
                table: "Track",
                column: "AlbumId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Track");

            migrationBuilder.DropTable(
                name: "Album");
        }
    }
}
