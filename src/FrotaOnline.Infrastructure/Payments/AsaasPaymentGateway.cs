using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FrotaOnline.Application.Abstractions.Payments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FrotaOnline.Infrastructure.Payments;

/// <summary>
/// Asaas REST API v3. Authentication is the <c>access_token</c> header; new accounts also need a <c>User-Agent</c>.
/// A refusal or a network failure becomes a <see cref="PaymentGatewayException"/>; the provider's answer is logged here,
/// never sent back to the app, because it can name internal ids.
/// </summary>
internal sealed class AsaasPaymentGateway : IPaymentGateway
{
    // Asaas answers Pix expiry in Brasília time ("2026-10-11 23:59:59"); Brazil has had no daylight saving since 2019.
    private static readonly TimeSpan BrasiliaOffset = TimeSpan.FromHours(-3);

    private readonly HttpClient _http;
    private readonly ILogger<AsaasPaymentGateway> _logger;

    public AsaasPaymentGateway(HttpClient http, IOptions<PaymentOptions> options, ILogger<AsaasPaymentGateway> logger)
    {
        _http = http;
        _logger = logger;

        var asaas = options.Value.Asaas;
        _http.BaseAddress = new Uri(asaas.BaseUrl.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.Add("access_token", asaas.ApiKey);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("FrotaOnline");
    }

    public bool IsConfigured => true;

    public async Task<string> CreateCustomerAsync(GatewayCustomer customer, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "customers", new Dictionary<string, object?>
        {
            ["name"] = customer.Name,
            ["email"] = customer.Email,
            ["cpfCnpj"] = customer.TaxId,
            ["mobilePhone"] = NormalizePhone(customer.Phone),
            ["externalReference"] = customer.ExternalReference
        }, cancellationToken);
        return ReadId(response);
    }

    public async Task<GatewaySubscription> CreateSubscriptionAsync(
        string customerId, decimal value, DateOnly firstDueDate, string description, string externalReference,
        CancellationToken cancellationToken)
    {
        // UNDEFINED: the driver chooses Pix, boleto or card on the provider's page, so no payment data passes through us.
        using var created = await SendAsync(HttpMethod.Post, "subscriptions", new Dictionary<string, object?>
        {
            ["customer"] = customerId,
            ["billingType"] = "UNDEFINED",
            ["value"] = value,
            ["nextDueDate"] = firstDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["cycle"] = "MONTHLY",
            ["description"] = description,
            ["externalReference"] = externalReference
        }, cancellationToken);
        var id = ReadId(created);

        // The first charge is created together with the subscription; its hosted page is where the driver pays.
        string? paymentUrl = null;
        try
        {
            using var payments = await SendAsync(HttpMethod.Get, $"subscriptions/{id}/payments", null, cancellationToken);
            using var document = await ParseAsync(payments, cancellationToken);
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0
                && data[0].TryGetProperty("invoiceUrl", out var url))
            {
                paymentUrl = url.GetString();
            }
        }
        catch (PaymentGatewayException)
        {
            // The subscription exists; the provider also emails the payment link to the driver.
            _logger.LogWarning("Asaas subscription {SubscriptionId} was created but its first charge could not be read.", id);
        }

        return new GatewaySubscription(id, paymentUrl);
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"subscriptions/{subscriptionId}", null, cancellationToken, allowNotFound: true);
    }

    public async Task<PixCharge> CreatePixChargeAsync(PixChargeRequest request, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["customer"] = request.CustomerId,
            ["billingType"] = "PIX",
            ["value"] = request.Value,
            ["dueDate"] = request.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["description"] = request.Description,
            ["externalReference"] = request.ExternalReference
        };

        if (request.Splits.Count > 0)
        {
            body["split"] = request.Splits
                .Select(s => new Dictionary<string, object?> { ["walletId"] = s.WalletId, ["fixedValue"] = s.FixedValue })
                .ToList();
        }

        using var created = await SendAsync(HttpMethod.Post, "payments", body, cancellationToken);
        var id = ReadId(created);

        using var qr = await SendAsync(HttpMethod.Get, $"payments/{id}/pixQrCode", null, cancellationToken);
        using var document = await ParseAsync(qr, cancellationToken);
        var root = document.RootElement;

        var payload = root.TryGetProperty("payload", out var p) ? p.GetString() : null;
        if (string.IsNullOrWhiteSpace(payload))
        {
            _logger.LogError("Asaas returned no Pix payload for charge {ChargeId}.", id);
            throw new PaymentGatewayException("The provider returned no Pix code.");
        }

        var image = root.TryGetProperty("encodedImage", out var e) ? e.GetString() : null;
        return new PixCharge(id, payload, image, ParseExpiry(root));
    }

    public async Task CancelChargeAsync(string chargeId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"payments/{chargeId}", null, cancellationToken, allowNotFound: true);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken, bool allowNotFound = false)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Asaas {Method} {Path} failed before an answer.", method, path);
            throw new PaymentGatewayException("The payment provider could not be reached.", ex);
        }

        if (response.IsSuccessStatusCode || (allowNotFound && response.StatusCode == System.Net.HttpStatusCode.NotFound))
        {
            return response;
        }

        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogError("Asaas {Method} {Path} answered {Status}: {Body}", method, path, (int)response.StatusCode, text);
        response.Dispose();
        throw new PaymentGatewayException($"The payment provider refused the request ({(int)response.StatusCode}).");
    }

    private static async Task<JsonDocument> ParseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static string ReadId(HttpResponseMessage response)
    {
        using var stream = response.Content.ReadAsStream();
        using var document = JsonDocument.Parse(stream);
        if (document.RootElement.TryGetProperty("id", out var id) && !string.IsNullOrWhiteSpace(id.GetString()))
        {
            return id.GetString()!;
        }

        throw new PaymentGatewayException("The provider answer has no id.");
    }

    private static DateTime ParseExpiry(JsonElement root)
    {
        if (root.TryGetProperty("expirationDate", out var value)
            && DateTime.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), BrasiliaOffset).UtcDateTime;
        }

        return DateTime.UtcNow.AddHours(24);
    }

    /// <summary>Asaas wants area code + number, digits only: "+5511999999999" becomes "11999999999".</summary>
    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var digits = new StringBuilder();
        foreach (var c in phone.Where(char.IsDigit))
        {
            digits.Append(c);
        }

        var text = digits.ToString();
        return text.Length > 11 && text.StartsWith("55", StringComparison.Ordinal) ? text[2..] : text;
    }
}
