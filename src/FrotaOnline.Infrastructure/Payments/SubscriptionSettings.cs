using FrotaOnline.Application.Abstractions.Payments;
using Microsoft.Extensions.Configuration;

namespace FrotaOnline.Infrastructure.Payments;

internal sealed class SubscriptionSettings : ISubscriptionSettings
{
    public SubscriptionSettings(IConfiguration configuration) =>
        RequiredToPropose = configuration.GetValue<bool>("Subscriptions:RequiredToPropose");

    public bool RequiredToPropose { get; }
}
