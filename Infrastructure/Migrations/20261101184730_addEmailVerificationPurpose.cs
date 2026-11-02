using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class addEmailVerificationPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NewEmail",
                table: "EmailVerificationCodes",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "EmailVerificationCodes",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Registration");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerificationCodes_UserId_Purpose",
                table: "EmailVerificationCodes",
                columns: new[] { "UserId", "Purpose" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailVerificationCodes_UserId_Purpose",
                table: "EmailVerificationCodes");

            migrationBuilder.DropColumn(
                name: "NewEmail",
                table: "EmailVerificationCodes");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "EmailVerificationCodes");
        }
    }
}
