using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SymDirs.Migrations
{
    /// <inheritdoc />
    public partial class many_to_many : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SyncedConfigDirectories_Files_DbFileId",
                table: "SyncedConfigDirectories");

            migrationBuilder.DropIndex(
                name: "IX_SyncedConfigDirectories_DbFileId",
                table: "SyncedConfigDirectories");

            migrationBuilder.DropColumn(
                name: "DbFileId",
                table: "SyncedConfigDirectories");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DbFileSyncedConfigDirectory");

            migrationBuilder.AddColumn<string>(
                name: "DbFileId",
                table: "SyncedConfigDirectories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncedConfigDirectories_DbFileId",
                table: "SyncedConfigDirectories",
                column: "DbFileId");

            migrationBuilder.AddForeignKey(
                name: "FK_SyncedConfigDirectories_Files_DbFileId",
                table: "SyncedConfigDirectories",
                column: "DbFileId",
                principalTable: "Files",
                principalColumn: "Id");
        }
    }
}
