using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace EfCoreMigrations.Migrations
{
    /// <inheritdoc />
    public partial class SeedGenreReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "genre_id",
                table: "books",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "genres",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_genres", x => x.id);
                });

            migrationBuilder.InsertData(
                table: "genres",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { 1, "Fiction" },
                    { 2, "Science Fiction" },
                    { 3, "Fantasy" },
                    { 4, "Non-Fiction" },
                    { 5, "Biography" },
                    { 6, "Mystery" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_books_genre_id",
                table: "books",
                column: "genre_id");

            migrationBuilder.AddForeignKey(
                name: "fk_books_genres_genre_id",
                table: "books",
                column: "genre_id",
                principalTable: "genres",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_books_genres_genre_id",
                table: "books");

            migrationBuilder.DropTable(
                name: "genres");

            migrationBuilder.DropIndex(
                name: "ix_books_genre_id",
                table: "books");

            migrationBuilder.DropColumn(
                name: "genre_id",
                table: "books");
        }
    }
}
