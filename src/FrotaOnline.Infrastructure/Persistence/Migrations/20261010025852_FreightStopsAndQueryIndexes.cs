using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrotaOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FreightStopsAndQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FreightRequests_Addresses_DeliveryAddressId",
                table: "FreightRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_FreightRequests_Addresses_PickupAddressId",
                table: "FreightRequests");

            migrationBuilder.DropIndex(
                name: "IX_FreightRequests_DeliveryAddressId",
                table: "FreightRequests");

            migrationBuilder.DropIndex(
                name: "IX_FreightRequests_PickupAddressId",
                table: "FreightRequests");

            migrationBuilder.DropColumn(
                name: "DeliveryAddressId",
                table: "FreightRequests");

            migrationBuilder.DropColumn(
                name: "PickupAddressId",
                table: "FreightRequests");

            migrationBuilder.CreateTable(
                name: "FreightStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FreightRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AddressId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreightStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FreightStops_Addresses_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Addresses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FreightStops_FreightRequests_FreightRequestId",
                        column: x => x.FreightRequestId,
                        principalTable: "FreightRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FreightItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FreightStopId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FreightItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FreightItems_FreightStops_FreightStopId",
                        column: x => x.FreightStopId,
                        principalTable: "FreightStops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_Status_ExpiresAt",
                table: "Proposals",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FreightItems_FreightStopId",
                table: "FreightItems",
                column: "FreightStopId");

            migrationBuilder.CreateIndex(
                name: "IX_FreightStops_AddressId",
                table: "FreightStops",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_FreightStops_FreightRequestId_Sequence",
                table: "FreightStops",
                columns: new[] { "FreightRequestId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FreightItems");

            migrationBuilder.DropTable(
                name: "FreightStops");

            migrationBuilder.DropIndex(
                name: "IX_Proposals_Status_ExpiresAt",
                table: "Proposals");

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryAddressId",
                table: "FreightRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "PickupAddressId",
                table: "FreightRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_FreightRequests_DeliveryAddressId",
                table: "FreightRequests",
                column: "DeliveryAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_FreightRequests_PickupAddressId",
                table: "FreightRequests",
                column: "PickupAddressId");

            migrationBuilder.AddForeignKey(
                name: "FK_FreightRequests_Addresses_DeliveryAddressId",
                table: "FreightRequests",
                column: "DeliveryAddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FreightRequests_Addresses_PickupAddressId",
                table: "FreightRequests",
                column: "PickupAddressId",
                principalTable: "Addresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
