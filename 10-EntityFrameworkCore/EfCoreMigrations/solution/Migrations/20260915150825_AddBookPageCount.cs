using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddBookPageCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "page_count",
                table: "books",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "page_count",
                table: "books");
        }
    }
}
