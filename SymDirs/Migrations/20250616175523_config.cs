using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SymDirs.Migrations
{
    /// <inheritdoc />
    public partial class config : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SyncedConfigDirectory_Files_DbFileId",
                table: "SyncedConfigDirectory");

            migrationBuilder.DropForeignKey(
                name: "FK_SyncedConfigDirectory_LocalDirectory_LocalDirectoryId",
                table: "SyncedConfigDirectory");

            migrationBuilder.DropTable(
                name: "LocalDirectory");

            migrationBuilder.DropTable(
                name: "SyncedConfigParentDirectory");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SyncedConfigDirectory",
                table: "SyncedConfigDirectory");

            migrationBuilder.DropIndex(
                name: "IX_SyncedConfigDirectory_LocalDirectoryId",
                table: "SyncedConfigDirectory");

            migrationBuilder.DropColumn(
                name: "LocalDirectoryId",
                table: "SyncedConfigDirectory");

            migrationBuilder.RenameTable(
                name: "SyncedConfigDirectory",
                newName: "SyncedConfigDirectories");

            migrationBuilder.RenameIndex(
                name: "IX_SyncedConfigDirectory_DbFileId",
                table: "SyncedConfigDirectories",
                newName: "IX_SyncedConfigDirectories_DbFileId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SyncedConfigDirectories",
                table: "SyncedConfigDirectories",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SyncedConfigDirectories_Files_DbFileId",
                table: "SyncedConfigDirectories",
                column: "DbFileId",
                principalTable: "Files",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SyncedConfigDirectories_Files_DbFileId",
                table: "SyncedConfigDirectories");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SyncedConfigDirectories",
                table: "SyncedConfigDirectories");

            migrationBuilder.RenameTable(
                name: "SyncedConfigDirectories",
                newName: "SyncedConfigDirectory");

            migrationBuilder.RenameIndex(
                name: "IX_SyncedConfigDirectories_DbFileId",
                table: "SyncedConfigDirectory",
                newName: "IX_SyncedConfigDirectory_DbFileId");

            migrationBuilder.AddColumn<string>(
                name: "LocalDirectoryId",
                table: "SyncedConfigDirectory",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SyncedConfigDirectory",
                table: "SyncedConfigDirectory",
                column: "Id");

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
                name: "SyncedConfigParentDirectory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    SyncedConfigDirectoryId = table.Column<string>(type: "TEXT", nullable: true),
                    childPath = table.Column<string>(type: "TEXT", nullable: false)
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
                name: "IX_SyncedConfigDirectory_LocalDirectoryId",
                table: "SyncedConfigDirectory",
                column: "LocalDirectoryId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncedConfigParentDirectory_SyncedConfigDirectoryId",
                table: "SyncedConfigParentDirectory",
                column: "SyncedConfigDirectoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_SyncedConfigDirectory_Files_DbFileId",
                table: "SyncedConfigDirectory",
                column: "DbFileId",
                principalTable: "Files",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SyncedConfigDirectory_LocalDirectory_LocalDirectoryId",
                table: "SyncedConfigDirectory",
                column: "LocalDirectoryId",
                principalTable: "LocalDirectory",
                principalColumn: "Id");
        }
    }
}
