using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SymDirs.Migrations
{
    /// <inheritdoc />
    public partial class MergeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_DbFile",
                table: "DbFile");

            migrationBuilder.RenameTable(
                name: "DbFile",
                newName: "Files");

            migrationBuilder.RenameIndex(
                name: "IX_DbFile_FullPath",
                table: "Files",
                newName: "IX_Files_FullPath");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Files",
                table: "Files",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Files_LastSync",
                table: "Files",
                column: "LastSync");

            migrationBuilder.CreateIndex(
                name: "IX_Files_LastSync_FullPath",
                table: "Files",
                columns: new[] { "LastSync", "FullPath" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Files",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_LastSync",
                table: "Files");

            migrationBuilder.DropIndex(
                name: "IX_Files_LastSync_FullPath",
                table: "Files");

            migrationBuilder.RenameTable(
                name: "Files",
                newName: "DbFile");

            migrationBuilder.RenameIndex(
                name: "IX_Files_FullPath",
                table: "DbFile",
                newName: "IX_DbFile_FullPath");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DbFile",
                table: "DbFile",
                column: "Id");
        }
    }
}
