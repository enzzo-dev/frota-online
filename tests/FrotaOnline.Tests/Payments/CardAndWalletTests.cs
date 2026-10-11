using FrotaOnline.Application.Payments.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Payments;

public class CardAndWalletTests : PaymentTestBase
{
    // 4111 1111 1111 1111 passes the Luhn check.
    private static readonly CardInput Visa = new("MARIA CLIENTE", "4111 1111 1111 1111", "12", "2030", "123");
    private static readonly CardHolderInput Holder = new(CustomerCpf, "01310-100", "1000", null);

    private static PayWithCardRequest NewCard(bool save = false) => new(null, Visa, Holder, save);

    // ---- card at Asaas ----

    [Fact]
    public async Task Card_ChargesWithTheSplit_AndStaysPendingUntilTheProviderConfirms()
    {
        var (_, driver, deliveryId) = await DeliverAsync();

        var result = await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Pending, result.Value.Status);
        Assert.Equal(145.10m, result.Value.DriverReceives);
        var charge = Assert.Single(Gateway.CardCharges);
        Assert.Equal(150m, charge.Value);
        Assert.Equal("203.0.113.7", charge.RemoteIp);
        Assert.Equal("4111111111111111", charge.Card!.Number);
        Assert.Equal("01310100", charge.Holder!.PostalCode);
        Assert.Equal(driver.PayoutWalletId, Assert.Single(charge.Splits).WalletId);
        var payment = Assert.Single(Payments.Items);
        Assert.Equal(PaymentMethod.Card, payment.Method);
        Assert.Equal(PaymentProviders.Asaas, payment.Gateway);
    }

    [Fact]
    public async Task Card_IsSettledByTheWebhook_LikeAPix()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);

        await Webhooks.HandleAsync(Paid("evt_1", Payments.Items.Single().GatewayPaymentId!, @event: "PAYMENT_CONFIRMED"), CancellationToken.None);

        Assert.Equal(PaymentStatus.Paid, Payments.Items.Single().Status);
        Assert.Equal(0m, await Fees.GetBalanceAsync(driver.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Card_Declined_ChargesNothing_AndKeepsNoPayment()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        Gateway.DeclineCards = true;

        var result = await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);

        Assert.Equal("payments.card_declined", result.Error!.Code);
        Assert.Empty(Payments.Items);
    }

    [Fact]
    public async Task Card_RefusedAfterwards_FailsThePayment_AndAllowsANewAttempt()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);
        SentNotifications.Items.Clear();

        await Webhooks.HandleAsync(
            Paid("evt_1", Payments.Items.Single().GatewayPaymentId!, @event: "PAYMENT_CREDIT_CARD_CAPTURE_REFUSED"), CancellationToken.None);
        var again = await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);

        Assert.Contains(SentNotifications.Items, n => n.UserId == CustomerId && n.Type == NotificationType.PaymentFailed);
        Assert.True(again.IsSuccess);
        Assert.Single(Payments.Items);
        Assert.Equal(PaymentStatus.Pending, Payments.Items[0].Status);
    }

    [Fact]
    public async Task Card_WhileAnotherCardPaymentIsBeingConfirmed_IsRejected()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);

        var second = await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);

        Assert.Equal("payments.in_progress", second.Error!.Code);
        Assert.Single(Gateway.CardCharges);
    }

    [Fact]
    public async Task Card_AfterAPixWasIssued_ReplacesIt_AndCancelsThePix()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        await PixService.CreateForDeliveryAsync(CustomerId, deliveryId, new CreatePixRequest(CustomerCpf), CancellationToken.None);
        var pixId = Payments.Items.Single().GatewayPaymentId!;

        var result = await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(pixId, Gateway.CancelledCharges);
        var payment = Assert.Single(Payments.Items);
        Assert.NotEqual(pixId, payment.GatewayPaymentId);
        Assert.Null(payment.PixPayload);
    }

    [Fact]
    public async Task Card_WithAnInvalidNumber_IsRejectedBeforeCallingTheProvider()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        var request = new PayWithCardRequest(null, Visa with { Number = "4111 1111 1111 1112" }, Holder, false);

        var result = await CardService.PayAsync(CustomerId, deliveryId, request, "203.0.113.7", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(Gateway.CardCharges);
        Assert.Empty(Gateway.Customers);
    }

    [Fact]
    public async Task Card_NeedsExactlyOneOfSavedCardOrNewCard()
    {
        var (_, _, deliveryId) = await DeliverAsync();

        var neither = await CardService.PayAsync(CustomerId, deliveryId, new PayWithCardRequest(null, null, null, false), "1.1.1.1", CancellationToken.None);
        var both = await CardService.PayAsync(CustomerId, deliveryId, new PayWithCardRequest(Guid.NewGuid(), Visa, Holder, false), "1.1.1.1", CancellationToken.None);

        Assert.True(neither.IsFailure);
        Assert.True(both.IsFailure);
    }

    [Fact]
    public async Task Card_WithoutPayoutAccount_ExplainsWhy()
    {
        var (_, _, deliveryId) = await DeliverAsync(linkWallet: false);

        var result = await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);

        Assert.Equal("payments.driver_without_payout_account", result.Error!.Code);
        Assert.Empty(Gateway.CardCharges);
    }

    // ---- saved cards ----

    [Fact]
    public async Task Card_SavedOnRequest_KeepsOnlyTheTokenBrandAndLastDigits()
    {
        var (_, _, deliveryId) = await DeliverAsync();

        var result = await CardService.PayAsync(CustomerId, deliveryId, NewCard(save: true), "203.0.113.7", CancellationToken.None);

        Assert.True(result.Value.CardSaved);
        var saved = Assert.Single(SavedCards.Items);
        Assert.StartsWith("tok_", saved.Token);
        Assert.Equal("VISA", saved.Brand);
        Assert.Equal("1111", saved.Last4);
        Assert.DoesNotContain("4111", saved.Token);
    }

    [Fact]
    public async Task Card_NotSavedUnlessAsked()
    {
        var (_, _, deliveryId) = await DeliverAsync();

        var result = await CardService.PayAsync(CustomerId, deliveryId, NewCard(save: false), "203.0.113.7", CancellationToken.None);

        Assert.False(result.Value.CardSaved);
        Assert.Empty(SavedCards.Items);
    }

    [Fact]
    public async Task Card_WithASavedCard_ChargesTheTokenOfThatProviderCustomer_WithoutCardData()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        SavedCards.Items.Add(SavedCard.Create(CustomerId, "cus_saved", "tok_saved", "master", "4444"));
        var cardId = SavedCards.Items[0].Id;

        var result = await CardService.PayAsync(CustomerId, deliveryId, new PayWithCardRequest(cardId, null, null, false), "203.0.113.7", CancellationToken.None);

        Assert.True(result.IsSuccess);
        var charge = Assert.Single(Gateway.CardCharges);
        Assert.Equal("cus_saved", charge.CustomerId);
        Assert.Equal("tok_saved", charge.CardToken);
        Assert.Null(charge.Card);
        Assert.Empty(Gateway.Customers);
    }

    [Fact]
    public async Task Card_SavedCardOfAnotherPerson_IsNotFound()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        var other = SavedCard.Create(Guid.NewGuid(), "cus_x", "tok_x", "visa", "9999");
        SavedCards.Items.Add(other);

        var result = await CardService.PayAsync(CustomerId, deliveryId, new PayWithCardRequest(other.Id, null, null, false), "1.1.1.1", CancellationToken.None);

        Assert.Equal("payments.card_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task Card_KeepsAtMostFiveSavedCards()
    {
        var (_, _, deliveryId) = await DeliverAsync();
        for (var i = 0; i < SavedCard.MaxPerUser; i++)
        {
            SavedCards.Items.Add(SavedCard.Create(CustomerId, $"cus_{i}", $"tok_{i}", "visa", "0000"));
        }

        var result = await CardService.PayAsync(CustomerId, deliveryId, NewCard(save: true), "203.0.113.7", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.CardSaved);
        Assert.Equal(SavedCard.MaxPerUser, SavedCards.Items.Count);
    }

    [Fact]
    public async Task SavedCards_CanBeListedAndRemoved()
    {
        var card = SavedCard.Create(CustomerId, "cus_1", "tok_1", "visa", "1111");
        SavedCards.Items.Add(card);

        var list = await CardService.ListCardsAsync(CustomerId, CancellationToken.None);
        var removed = await CardService.RemoveCardAsync(CustomerId, card.Id, CancellationToken.None);
        var again = await CardService.RemoveCardAsync(CustomerId, card.Id, CancellationToken.None);

        Assert.Equal("1111", Assert.Single(list.Value).Last4);
        Assert.True(removed.IsSuccess);
        Assert.Equal("payments.card_not_found", again.Error!.Code);
    }

    // ---- connecting the driver's Mercado Pago account ----

    [Fact]
    public async Task WalletAccount_Connect_StoresTheTokensEncrypted()
    {
        var (driverUser, driver, _) = await DeliverAsync();
        var start = await WalletAccountService.StartAuthorizationAsync(driverUser, CancellationToken.None);
        var state = start.Value.AuthorizationUrl.Split("state=")[1];

        var done = await WalletAccountService.CompleteAuthorizationAsync("code-1", state, CancellationToken.None);

        Assert.True(done.IsSuccess);
        var account = Assert.Single(WalletAccounts.Items);
        Assert.Equal(driver.Id, account.DriverProfileId);
        Assert.Equal("enc:access-1", account.AccessTokenProtected);
        Assert.NotEqual("access-1", account.AccessTokenProtected);
        Assert.True((await WalletAccountService.GetAsync(driverUser, CancellationToken.None)).Value.Connected);
    }

    [Fact]
    public async Task WalletAccount_WithATamperedOrExpiredState_IsRejected()
    {
        var (driverUser, _, _) = await DeliverAsync();
        var start = await WalletAccountService.StartAuthorizationAsync(driverUser, CancellationToken.None);
        var state = start.Value.AuthorizationUrl.Split("state=")[1];

        var tampered = await WalletAccountService.CompleteAuthorizationAsync("code-1", "garbage", CancellationToken.None);
        Clock.Advance(TimeSpan.FromMinutes(20));
        var expired = await WalletAccountService.CompleteAuthorizationAsync("code-1", state, CancellationToken.None);

        Assert.Equal("wallet.authorization_invalid", tampered.Error!.Code);
        Assert.Equal("wallet.authorization_invalid", expired.Error!.Code);
        Assert.Empty(WalletAccounts.Items);
    }

    [Fact]
    public async Task WalletAccount_Reconnecting_ReplacesTheTokens()
    {
        var (driverUser, _, _) = await DeliverAsync();
        for (var i = 0; i < 2; i++)
        {
            var start = await WalletAccountService.StartAuthorizationAsync(driverUser, CancellationToken.None);
            await WalletAccountService.CompleteAuthorizationAsync("code", start.Value.AuthorizationUrl.Split("state=")[1], CancellationToken.None);
        }

        Assert.Single(WalletAccounts.Items);
    }

    [Fact]
    public async Task WalletAccount_Disconnect_RemovesIt()
    {
        var (driverUser, driver, _) = await DeliverAsync();
        ConnectWallet(driver.Id);

        var result = await WalletAccountService.DisconnectAsync(driverUser, CancellationToken.None);

        Assert.False(result.Value.Connected);
        Assert.Empty(WalletAccounts.Items);
    }

    // ---- wallet payment (Apple Pay token) ----

    private static readonly WalletPaymentRequest ApplePayToken = new("card-token-1", "visa");

    private void ConnectWallet(Guid driverProfileId, DateTime? expiresAt = null) =>
        WalletAccounts.Items.Add(WalletAccount.Create(
            driverProfileId, "mp-user-1", Secrets.Protect("access-1"), Secrets.Protect("refresh-1"),
            expiresAt ?? Clock.UtcNow.AddDays(90)));

    [Fact]
    public async Task Wallet_Approved_SettlesAtOnce_WithTheDriversTokenAndThePlatformFee()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id);

        var result = await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, result.Value.Status);
        var charge = Assert.Single(Wallet.Charges);
        Assert.Equal("access-1", charge.SellerAccessToken);
        Assert.Equal(4.90m, charge.PlatformFee);
        Assert.Equal(150m, charge.Value);
        Assert.Equal("maria@example.com", charge.PayerEmail);
        Assert.Equal(PaymentProviders.MercadoPago, Payments.Items.Single().Gateway);
        Assert.Equal(0m, await Fees.GetBalanceAsync(driver.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Wallet_InProcess_StaysPending_UntilTheNotificationConfirms()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id);
        Wallet.Status = "in_process";
        var result = await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);
        Assert.Equal(PaymentStatus.Pending, result.Value.Status);

        Wallet.Status = "approved";
        await WalletService.ReconcileAsync(Payments.Items.Single().GatewayPaymentId!, CancellationToken.None);

        Assert.Equal(PaymentStatus.Paid, Payments.Items.Single().Status);
        Assert.Equal(0m, await Fees.GetBalanceAsync(driver.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Wallet_InProcessThenRejected_FailsThePayment()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id);
        Wallet.Status = "in_process";
        await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);

        Wallet.Status = "rejected";
        await WalletService.ReconcileAsync(Payments.Items.Single().GatewayPaymentId!, CancellationToken.None);

        Assert.Equal(PaymentStatus.Failed, Payments.Items.Single().Status);
    }

    [Fact]
    public async Task Wallet_Reconcile_TwiceForTheSameApproval_CountsTheMoneyOnce()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id);
        Wallet.Status = "in_process";
        await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);
        Wallet.Status = "approved";
        var id = Payments.Items.Single().GatewayPaymentId!;

        await WalletService.ReconcileAsync(id, CancellationToken.None);
        await WalletService.ReconcileAsync(id, CancellationToken.None);

        Assert.Single(Fees.Items, e => e.EntryType == FeeEntryType.FeeSettled);
    }

    [Fact]
    public async Task Wallet_Rejected_ChargesNothing_AndKeepsNoPayment()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id);
        Wallet.Declines = true;

        var result = await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);

        Assert.Equal("payments.card_declined", result.Error!.Code);
        Assert.Empty(Payments.Items);
    }

    [Fact]
    public async Task Wallet_DriverWithoutConnectedAccount_ExplainsWhy()
    {
        var (_, _, deliveryId) = await DeliverAsync();

        var result = await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);

        Assert.Equal("payments.driver_without_wallet_account", result.Error!.Code);
        Assert.Empty(Wallet.Charges);
    }

    [Fact]
    public async Task Wallet_NotConfigured_AnswersNotConfigured()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id);
        Wallet.IsConfigured = false;

        var result = await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);

        Assert.Equal("payments.not_configured", result.Error!.Code);
    }

    [Fact]
    public async Task Wallet_ProviderDown_ReturnsUnavailable()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id);
        Wallet.Fails = true;

        var result = await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);

        Assert.Equal("payments.unavailable", result.Error!.Code);
        Assert.Empty(Payments.Items);
    }

    [Fact]
    public async Task Wallet_RefreshesATokenThatIsAboutToExpire_BeforeCharging()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id, expiresAt: Clock.UtcNow.AddHours(5));

        var result = await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("refresh-1", Assert.Single(Wallet.Refreshed));
        Assert.Equal("access-2", Wallet.Charges.Single().SellerAccessToken);
        Assert.Equal("enc:refresh-2", WalletAccounts.Items.Single().RefreshTokenProtected);
    }

    [Fact]
    public async Task Wallet_SameTokenForTheSameDelivery_UsesTheSameIdempotencyKey()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id);
        Wallet.Status = "in_process";
        await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);
        Payments.Items.Single().MarkFailed();
        await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);

        Assert.Equal(2, Wallet.Charges.Count);
        Assert.Equal(Wallet.Charges[0].IdempotencyKey, Wallet.Charges[1].IdempotencyKey);
        Assert.Equal(40, Wallet.Charges[0].IdempotencyKey.Length);
    }

    [Fact]
    public async Task Wallet_WhileACardIsBeingConfirmed_IsRejected()
    {
        var (_, driver, deliveryId) = await DeliverAsync();
        ConnectWallet(driver.Id);
        await CardService.PayAsync(CustomerId, deliveryId, NewCard(), "203.0.113.7", CancellationToken.None);

        var result = await WalletService.PayAsync(CustomerId, deliveryId, ApplePayToken, CancellationToken.None);

        Assert.Equal("payments.in_progress", result.Error!.Code);
    }
}
