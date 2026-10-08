using FrotaOnline.Application.Abstractions;

namespace FrotaOnline.Infrastructure.Services;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
