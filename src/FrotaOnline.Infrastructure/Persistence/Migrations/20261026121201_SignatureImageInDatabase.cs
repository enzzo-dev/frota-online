using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrotaOnline.Infrastructure.Persistence.Migrations
{
    public partial class SignatureImageInDatabase : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"Signatures\";");

            migrationBuilder.DropIndex(
                name: "IX_Signatures_StoragePath",
                table: "Signatures");

            migrationBuilder.DropColumn(
                name: "StoragePath",
                table: "Signatures");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "Signatures",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "ImageData",
                table: "Signatures",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "SizeBytes",
                table: "Signatures",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "Signatures");

            migrationBuilder.DropColumn(
                name: "ImageData",
                table: "Signatures");

            migrationBuilder.DropColumn(
                name: "SizeBytes",
                table: "Signatures");

            migrationBuilder.AddColumn<string>(
                name: "StoragePath",
                table: "Signatures",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Signatures_StoragePath",
                table: "Signatures",
                column: "StoragePath",
                unique: true);
        }
    }
}
