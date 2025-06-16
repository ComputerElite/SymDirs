using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SymDirs.Migrations
{
    /// <inheritdoc />
    public partial class seperate_config_dir_type : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DbFileSyncedConfigDirectory");

            migrationBuilder.DropTable(
                name: "SyncedConfigDirectories");

            migrationBuilder.CreateTable(
                name: "ConfigDirectories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfigDirectories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DbConfigDirectoryDbFile",
                columns: table => new
                {
                    DbFileId = table.Column<string>(type: "TEXT", nullable: false),
                    SyncedWithId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbConfigDirectoryDbFile", x => new { x.DbFileId, x.SyncedWithId });
                    table.ForeignKey(
                        name: "FK_DbConfigDirectoryDbFile_ConfigDirectories_SyncedWithId",
                        column: x => x.SyncedWithId,
                        principalTable: "ConfigDirectories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DbConfigDirectoryDbFile_Files_DbFileId",
                        column: x => x.DbFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DbConfigDirectoryDbFile_SyncedWithId",
                table: "DbConfigDirectoryDbFile",
                column: "SyncedWithId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DbConfigDirectoryDbFile");

            migrationBuilder.DropTable(
                name: "ConfigDirectories");

            migrationBuilder.CreateTable(
                name: "SyncedConfigDirectories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    FolderName = table.Column<string>(type: "TEXT", nullable: false),
                    IsSourceDirectory = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncedConfigDirectories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DbFileSyncedConfigDirectory",
                columns: table => new
                {
                    DbFileId = table.Column<string>(type: "TEXT", nullable: false),
                    SyncedWithId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbFileSyncedConfigDirectory", x => new { x.DbFileId, x.SyncedWithId });
                    table.ForeignKey(
                        name: "FK_DbFileSyncedConfigDirectory_Files_DbFileId",
                        column: x => x.DbFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DbFileSyncedConfigDirectory_SyncedConfigDirectories_SyncedWithId",
                        column: x => x.SyncedWithId,
                        principalTable: "SyncedConfigDirectories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DbFileSyncedConfigDirectory_SyncedWithId",
                table: "DbFileSyncedConfigDirectory",
                column: "SyncedWithId");
        }
    }
}
