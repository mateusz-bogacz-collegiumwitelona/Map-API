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
                name: "IX_Users_PublicActive_CreatedAt",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PublicActive_Name",
                table: "Users");
        }
    }
}
