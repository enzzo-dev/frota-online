namespace FrotaOnline.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary><c>Asaas</c> or <c>None</c> (default: online payments off, local development without a provider account).</summary>
    public string Provider { get; init; } = "None";

    public AsaasOptions Asaas { get; init; } = new();

    /// <summary><c>MercadoPago</c> or <c>None</c> (default): Apple Pay / wallet payments and the driver's account connection.</summary>
    public string WalletProvider { get; init; } = "None";

    public MercadoPagoOptions MercadoPago { get; init; } = new();
}

public sealed class MercadoPagoOptions
{
    public string ApiUrl { get; init; } = "https://api.mercadopago.com";

    public string AuthUrl { get; init; } = "https://auth.mercadopago.com.br/authorization";

    /// <summary>Application id and secret (Mercado Pago "Suas integrações"). The secret is a secret.</summary>
    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>Public address of <c>GET /api/v1/callbacks/mercadopago/oauth</c>; must be registered in the application.</summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>Optional: where the browser goes after the driver connects the account (a deep link into the app).</summary>
    public string AppReturnUrl { get; init; } = string.Empty;

    /// <summary>Optional: address Mercado Pago calls for each payment (<c>POST /api/v1/webhooks/mercadopago</c>). Otherwise set in the panel.</summary>
    public string NotificationUrl { get; init; } = string.Empty;

    /// <summary>Secret of the webhook signature (panel: Webhooks). A secret.</summary>
    public string WebhookSecret { get; init; } = string.Empty;

    /// <summary>Base64 of 32 random bytes: encrypts the drivers' tokens in the database. A secret; losing it disconnects every driver.</summary>
    public string TokenEncryptionKey { get; init; } = string.Empty;
}

public sealed class AsaasOptions
{
    /// <summary>
    /// Sandbox: <c>https://api-sandbox.asaas.com/v3</c>. Production: <c>https://api.asaas.com/v3</c>. The Asaas
    /// documentation shows two addresses for the sandbox; use the one your sandbox account page gives.
    /// </summary>
    public string BaseUrl { get; init; } = "https://api-sandbox.asaas.com/v3";

    /// <summary>The API key of the platform's Asaas account (different for sandbox and production). Secret.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>The token configured in the Asaas webhook; Asaas sends it in the <c>asaas-access-token</c> header. 32 to 255 characters. Secret.</summary>
    public string WebhookToken { get; init; } = string.Empty;
}
