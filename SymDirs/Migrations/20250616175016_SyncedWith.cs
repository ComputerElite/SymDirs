using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SymDirs.Migrations
{
    /// <inheritdoc />
    public partial class SyncedWith : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalDirectory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalDirectory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncedConfigDirectory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FolderName = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    IsSourceDirectory = table.Column<bool>(type: "INTEGER", nullable: false),
                    LocalDirectoryId = table.Column<string>(type: "TEXT", nullable: true),
                    DbFileId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncedConfigDirectory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncedConfigDirectory_Files_DbFileId",
                        column: x => x.DbFileId,
                        principalTable: "Files",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SyncedConfigDirectory_LocalDirectory_LocalDirectoryId",
                        column: x => x.LocalDirectoryId,
                        principalTable: "LocalDirectory",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SyncedConfigParentDirectory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    childPath = table.Column<string>(type: "TEXT", nullable: false),
                    SyncedConfigDirectoryId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncedConfigParentDirectory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncedConfigParentDirectory_SyncedConfigDirectory_SyncedConfigDirectoryId",
                        column: x => x.SyncedConfigDirectoryId,
                        principalTable: "SyncedConfigDirectory",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncedConfigDirectory_DbFileId",
                table: "SyncedConfigDirectory",
                column: "DbFileId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncedConfigDirectory_LocalDirectoryId",
                table: "SyncedConfigDirectory",
                column: "LocalDirectoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncedConfigParentDirectory_SyncedConfigDirectoryId",
                table: "SyncedConfigParentDirectory",
                column: "SyncedConfigDirectoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncedConfigParentDirectory");

            migrationBuilder.DropTable(
                name: "SyncedConfigDirectory");

            migrationBuilder.DropTable(
                name: "LocalDirectory");
        }
    }
}
