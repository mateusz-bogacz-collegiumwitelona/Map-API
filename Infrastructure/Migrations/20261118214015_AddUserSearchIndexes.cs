using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_Users_FirstName_Trgm",
                table: "Users",
                column: "FirstName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastName_Trgm",
                table: "Users",
                column: "LastName")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PublicActive_CreatedAt",
                table: "Users",
                columns: new[] { "IsPublic", "IsDeleted", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_PublicActive_Name",
                table: "Users",
                columns: new[] { "IsPublic", "IsDeleted", "FirstName", "LastName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_FirstName_Trgm",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_LastName_Trgm",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PublicActive_CreatedAt",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PublicActive_Name",
                table: "Users");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}
