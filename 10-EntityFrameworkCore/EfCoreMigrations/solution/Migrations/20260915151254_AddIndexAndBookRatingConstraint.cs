using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EfCoreMigrations.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexAndBookRatingConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rating",
                table: "books",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_books_title",
                table: "books",
                column: "title");

            migrationBuilder.AddCheckConstraint(
                name: "ck_books_rating_range",
                table: "books",
                sql: "rating IS NULL OR (rating BETWEEN 1 AND 5)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_books_title",
                table: "books");

            migrationBuilder.DropCheckConstraint(
                name: "ck_books_rating_range",
                table: "books");

            migrationBuilder.DropColumn(
                name: "rating",
                table: "books");
        }
    }
}
