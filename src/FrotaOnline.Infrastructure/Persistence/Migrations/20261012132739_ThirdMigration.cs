using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FrotaOnline.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ThirdMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_DeliveryId",
                table: "Payments");

            migrationBuilder.AddColumn<string>(
                name: "GatewaySubscriptionId",
                table: "Subscriptions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentUrl",
                table: "Subscriptions",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gateway",
                table: "Payments",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GatewayPaymentId",
                table: "Payments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PixExpiresAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PixPayload",
                table: "Payments",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PixQrCode",
                table: "Payments",
                type: "character varying(16384)",
                maxLength: 16384,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RouteDistanceMeters",
                table: "FreightRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RouteDurationSeconds",
                table: "FreightRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentCustomerId",
                table: "DriverProfiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayoutWalletId",
                table: "DriverProfiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Addresses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FeeCharges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GatewayPaymentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PixPayload = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    PixQrCode = table.Column<string>(type: "character varying(16384)", maxLength: 16384, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeCharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeCharges_DriverProfiles_DriverProfileId",
                        column: x => x.DriverProfileId,
                        principalTable: "DriverProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NewFreightAlerts = table.Column<bool>(type: "boolean", nullable: false),
                    PushEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PushDispatchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PushAttempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SavedCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GatewayCustomerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Token = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Brand = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Last4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedCards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AccessTokenProtected = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    RefreshTokenProtected = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletAccounts_DriverProfiles_DriverProfileId",
                        column: x => x.DriverProfileId,
                        principalTable: "DriverProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WebhookEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_GatewaySubscriptionId",
                table: "Subscriptions",
                column: "GatewaySubscriptionId",
                filter: "\"GatewaySubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DeliveryId",
                table: "Payments",
                column: "DeliveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_GatewayPaymentId",
                table: "Payments",
                column: "GatewayPaymentId",
                filter: "\"GatewayPaymentId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DriverProfiles_PayoutWalletId",
                table: "DriverProfiles",
                column: "PayoutWalletId",
                unique: true,
                filter: "\"PayoutWalletId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FeeCharges_DriverProfileId_Status",
                table: "FeeCharges",
                columns: new[] { "DriverProfileId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeCharges_GatewayPaymentId",
                table: "FeeCharges",
                column: "GatewayPaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PendingPush",
                table: "Notifications",
                column: "CreatedAt",
                filter: "\"PushDispatchedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAt",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedCards_UserId",
                table: "SavedCards",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletAccounts_DriverProfileId",
                table: "WalletAccounts",
                column: "DriverProfileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WalletAccounts_ProviderUserId",
                table: "WalletAccounts",
                column: "ProviderUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookEvents_EventId",
                table: "WebhookEvents",
                column: "EventId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeeCharges");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "SavedCards");

            migrationBuilder.DropTable(
                name: "WalletAccounts");

            migrationBuilder.DropTable(
                name: "WebhookEvents");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_GatewaySubscriptionId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_DeliveryId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_GatewayPaymentId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_DriverProfiles_PayoutWalletId",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "GatewaySubscriptionId",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "PaymentUrl",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "Gateway",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "GatewayPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PixExpiresAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PixPayload",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PixQrCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RouteDistanceMeters",
                table: "FreightRequests");

            migrationBuilder.DropColumn(
                name: "RouteDurationSeconds",
                table: "FreightRequests");

            migrationBuilder.DropColumn(
                name: "PaymentCustomerId",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "PayoutWalletId",
                table: "DriverProfiles");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Addresses");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DeliveryId",
                table: "Payments",
                column: "DeliveryId");
        }
    }
}
