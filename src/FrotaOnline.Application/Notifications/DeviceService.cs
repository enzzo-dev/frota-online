using FluentValidation;
using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Notifications.Dtos;
using FrotaOnline.Domain.Entities;

namespace FrotaOnline.Application.Notifications;

public sealed class DeviceService : IDeviceService
{
    private readonly IDeviceTokenRepository _devices;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<RegisterDeviceRequest> _validator;

    public DeviceService(
        IDeviceTokenRepository devices, IUnitOfWork unitOfWork, IDateTimeProvider clock, IValidator<RegisterDeviceRequest> validator)
    {
        _devices = devices;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result> RegisterAsync(Guid userId, RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ValidationMapper.ToError(validation));
        }

        var token = request.Token.Trim();
        var now = _clock.UtcNow;
        var existing = await _devices.GetByTokenAsync(token, cancellationToken);
        if (existing is null)
        {
            await _devices.AddAsync(DeviceToken.Create(userId, token, request.Platform, now), cancellationToken);
        }
        else
        {
            // The same phone, now signed in as someone else: the token follows the latest login.
            existing.Reassign(userId, request.Platform, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UnregisterAsync(Guid userId, string? token, CancellationToken cancellationToken)
    {
        var existing = string.IsNullOrWhiteSpace(token)
            ? null
            : await _devices.GetByTokenAsync(token.Trim(), cancellationToken);

        if (existing is not null && existing.UserId == userId)
        {
            _devices.Remove(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
