using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrotaOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DriverTaxId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverProfiles_CpfNumber",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "CpfNumber",
                table: "DriverProfiles");

            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                table: "DriverProfiles",
                type: "character varying(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_TaxId",
                table: "DriverProfiles",
                column: "TaxId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DriverProfiles_TaxId",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "TaxId",
                table: "DriverProfiles");

            migrationBuilder.AddColumn<string>(
                name: "CpfNumber",
                table: "DriverProfiles",
                type: "character varying(11)",
                maxLength: 11,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_CpfNumber",
                table: "DriverProfiles",
                column: "CpfNumber",
                unique: true);
        }
    }
}
