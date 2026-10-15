using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrotaOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261015230000_DeliveryCustomerIndex")]
    public partial class DeliveryCustomerIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Deliveries_CustomerId",
                table: "Deliveries");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_CustomerId_CreatedAt",
                table: "Deliveries",
                columns: new[] { "CustomerId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Deliveries_CustomerId_CreatedAt",
                table: "Deliveries");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_CustomerId",
                table: "Deliveries",
                column: "CustomerId");
        }
    }
}
