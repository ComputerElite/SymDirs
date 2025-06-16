using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SymDirs.Migrations
{
    /// <inheritdoc />
    public partial class MapIsSynced_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSynced",
                table: "Files",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSynced",
                table: "Files");
        }
    }
}
