using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrotaOnline.Infrastructure.Persistence.Migrations
{
    public partial class RoutePolylineAndLocationFillFactor : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoutePolyline",
                table: "FreightRequests",
                type: "text",
                nullable: true);

            migrationBuilder.Sql("ALTER TABLE \"DeliveryLocations\" SET (fillfactor = 70);");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"DeliveryLocations\" RESET (fillfactor);");

            migrationBuilder.DropColumn(
                name: "RoutePolyline",
                table: "FreightRequests");
        }
    }
}
