using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreMigrations.Migrations
{
    /// <inheritdoc />
    public partial class RenameBookPagesColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "page_count",
                table: "books",
                newName: "pages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "pages",
                table: "books",
                newName: "page_count");
        }
    }
}
