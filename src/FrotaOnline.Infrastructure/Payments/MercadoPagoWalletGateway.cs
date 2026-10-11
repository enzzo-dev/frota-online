using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FrotaOnline.Application.Abstractions.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FrotaOnline.Infrastructure.Payments;

/// <summary>
/// Mercado Pago. The driver authorises their account with OAuth; a payment is then created with the driver's access token
/// and <c>application_fee</c>, so the driver is the seller and the platform keeps the fee. The card token comes from the
/// provider's SDK in the app (Apple Pay is turned into a card token there), so card data never reaches this server.
/// Provider answers are logged here, never sent back to the app.
/// </summary>
internal sealed class MercadoPagoWalletGateway : IWalletPaymentGateway
{
    private readonly HttpClient _http;
    private readonly MercadoPagoOptions _options;
    private readonly ILogger<MercadoPagoWalletGateway> _logger;

    public MercadoPagoWalletGateway(HttpClient http, IOptions<PaymentOptions> options, ILogger<MercadoPagoWalletGateway> logger)
    {
        _http = http;
        _logger = logger;
        _options = options.Value.MercadoPago;
        _http.BaseAddress = new Uri(_options.ApiUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    public bool IsConfigured => true;

    public string BuildAuthorizationUrl(string state) =>
        $"{_options.AuthUrl}?client_id={Uri.EscapeDataString(_options.ClientId)}&response_type=code&platform_id=mp"
        + $"&state={Uri.EscapeDataString(state)}&redirect_uri={Uri.EscapeDataString(_options.RedirectUri)}";

    public Task<SellerAuthorization> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
        RequestTokenAsync(new Dictionary<string, object?>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri
        }, cancellationToken);

    public Task<SellerAuthorization> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestTokenAsync(new Dictionary<string, object?>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        }, cancellationToken);

    public async Task<WalletCharge> ChargeAsync(WalletChargeRequest request, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["transaction_amount"] = request.Value,
            ["token"] = request.CardToken,
            ["description"] = request.Description,
            ["installments"] = 1,
            ["payment_method_id"] = request.PaymentMethodId,
            ["payer"] = new Dictionary<string, object?> { ["email"] = request.PayerEmail },
            // What the platform keeps; the rest of the payment goes to the seller (the driver), after the provider's own fee.
            ["application_fee"] = request.PlatformFee,
            ["external_reference"] = request.ExternalReference
        };

        if (!string.IsNullOrWhiteSpace(_options.NotificationUrl))
        {
            body["notification_url"] = _options.NotificationUrl;
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/payments") { Content = JsonContent.Create(body) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", request.SellerAccessToken);
        message.Headers.Add("X-Idempotency-Key", request.IdempotencyKey);

        using var response = await SendAsync(message, cancellationToken, declineOnBadRequest: true);
        var charge = await ReadChargeAsync(response, cancellationToken);
        if (charge.Status is "rejected" or "cancelled")
        {
            _logger.LogInformation("Mercado Pago payment {PaymentId} was {Status}.", charge.Id, charge.Status);
            throw new PaymentDeclinedException("The provider rejected the payment.");
        }

        return charge;
    }

    public async Task<WalletCharge> GetPaymentAsync(string paymentId, string sellerAccessToken, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"v1/payments/{Uri.EscapeDataString(paymentId)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sellerAccessToken);
        using var response = await SendAsync(message, cancellationToken);
        return await ReadChargeAsync(response, cancellationToken);
    }

    private async Task<SellerAuthorization> RequestTokenAsync(Dictionary<string, object?> body, CancellationToken cancellationToken)
    {
        body["client_id"] = _options.ClientId;
        body["client_secret"] = _options.ClientSecret;

        using var message = new HttpRequestMessage(HttpMethod.Post, "oauth/token") { Content = JsonContent.Create(body) };
        using var response = await SendAsync(message, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var access = root.TryGetProperty("access_token", out var a) ? a.GetString() : null;
        var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
        var userId = root.TryGetProperty("user_id", out var u) ? u.ToString() : null;
        var seconds = root.TryGetProperty("expires_in", out var e) && e.TryGetInt64(out var value) ? value : 15_552_000L;

        if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) || string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogError("Mercado Pago returned an incomplete OAuth answer.");
            throw new PaymentGatewayException("The provider answer is incomplete.");
        }

        return new SellerAuthorization(userId, access, refresh, DateTime.UtcNow.AddSeconds(seconds));
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage message, CancellationToken cancellationToken, bool declineOnBadRequest = false)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Mercado Pago {Method} {Path} failed before an answer.", message.Method, message.RequestUri);
            throw new PaymentGatewayException("The payment provider could not be reached.", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError(
            "Mercado Pago {Method} {Path} answered {Status}: {Body}", message.Method, message.RequestUri, (int)response.StatusCode, text);
        var declined = declineOnBadRequest && response.StatusCode == HttpStatusCode.BadRequest;
        response.Dispose();
        if (declined)
        {
            throw new PaymentDeclinedException("The provider did not accept the payment.");
        }

        throw new PaymentGatewayException($"The payment provider refused the request ({(int)response.StatusCode}).");
    }

    private static async Task<WalletCharge> ReadChargeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        // The payment id is a number.
        var id = root.TryGetProperty("id", out var idElement)
            ? idElement.ValueKind == JsonValueKind.Number ? idElement.GetInt64().ToString(CultureInfo.InvariantCulture) : idElement.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new PaymentGatewayException("The provider answer has no id.");
        }

        var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? string.Empty : string.Empty;
        return new WalletCharge(id, status);
    }
}
