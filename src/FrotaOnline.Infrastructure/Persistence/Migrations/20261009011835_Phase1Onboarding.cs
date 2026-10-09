using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrotaOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase1Onboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_OwnerUserId",
                table: "MediaFiles");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "MediaFiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "CpfNumber",
                table: "DriverProfiles",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "CnhNumber",
                table: "DriverProfiles",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "CnhCategory",
                table: "DriverProfiles",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_OwnerUserId_Kind",
                table: "MediaFiles",
                columns: new[] { "OwnerUserId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_StorageKey",
                table: "MediaFiles",
                column: "StorageKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_CnhNumber",
                table: "DriverProfiles",
                column: "CnhNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_CpfNumber",
                table: "DriverProfiles",
                column: "CpfNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_OwnerUserId_Kind",
                table: "MediaFiles");

            migrationBuilder.DropIndex(
                name: "IX_MediaFiles_StorageKey",
                table: "MediaFiles");

            migrationBuilder.DropIndex(
                name: "IX_DriverProfiles_CnhNumber",
                table: "DriverProfiles");

            migrationBuilder.DropIndex(
                name: "IX_DriverProfiles_CpfNumber",
                table: "DriverProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "ContentType",
                table: "MediaFiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "CpfNumber",
                table: "DriverProfiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11);

            migrationBuilder.AlterColumn<string>(
                name: "CnhNumber",
                table: "DriverProfiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(11)",
                oldMaxLength: 11);

            migrationBuilder.AlterColumn<string>(
                name: "CnhCategory",
                table: "DriverProfiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2)",
                oldMaxLength: 2);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_OwnerUserId",
                table: "MediaFiles",
                column: "OwnerUserId");
        }
    }
}
