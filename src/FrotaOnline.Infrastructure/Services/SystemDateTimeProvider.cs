using FrotaOnline.Application.Abstractions.Time;

namespace FrotaOnline.Infrastructure.Services;

internal sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
