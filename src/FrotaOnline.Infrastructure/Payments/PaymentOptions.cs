namespace FrotaOnline.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    /// <summary><c>Asaas</c> or <c>None</c> (default: online payments off, local development without a provider account).</summary>
    public string Provider { get; init; } = "None";

    public AsaasOptions Asaas { get; init; } = new();
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
