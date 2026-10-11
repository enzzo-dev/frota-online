using FluentValidation;
using FrotaOnline.Application.Abstractions.Drivers;
using FrotaOnline.Application.Abstractions.Notifications;
using FrotaOnline.Application.Abstractions.Persistence;
using FrotaOnline.Application.Abstractions.Queries;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Admin.Dtos;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Notifications;
using FrotaOnline.Application.Drivers.Dtos;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Admin;

public sealed class DriverReviewService : IDriverReviewService
{
    private static readonly MediaKind[] DriverKinds = { MediaKind.DriverDocument, MediaKind.VehiclePhoto };

    private readonly IDriverRepository _drivers;
    private readonly IDriverReviewQueries _queries;
    private readonly IUserRepository _users;
    private readonly IVehicleRepository _vehicles;
    private readonly IMediaFileRepository _media;
    private readonly IEmailSender _email;
    private readonly INotificationPublisher _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<RejectDriverRequest> _rejectValidator;

    public DriverReviewService(
        IDriverRepository drivers,
        IDriverReviewQueries queries,
        IUserRepository users,
        IVehicleRepository vehicles,
        IMediaFileRepository media,
        IEmailSender email,
        INotificationPublisher notifications,
        IUnitOfWork unitOfWork,
        IDateTimeProvider clock,
        IValidator<RejectDriverRequest> rejectValidator)
    {
        _drivers = drivers;
        _queries = queries;
        _users = users;
        _vehicles = vehicles;
        _media = media;
        _email = email;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _rejectValidator = rejectValidator;
    }

    public async Task<Result<IReadOnlyList<DriverReviewSummary>>> ListAsync(DriverStatus status, CancellationToken cancellationToken)
    {
        var summaries = await _queries.ListByStatusAsync(status, cancellationToken);
        return Result.Success(summaries);
    }

    public async Task<Result<DriverReviewDetail>> GetAsync(Guid driverId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByIdAsync(driverId, cancellationToken);
        var user = profile is null ? null : await _users.GetByIdAsync(profile.UserId, cancellationToken);
        if (profile is null || user is null)
        {
            return Result.Failure<DriverReviewDetail>(ReviewErrors.DriverNotFound);
        }

        var vehicles = await _vehicles.ListActiveByDriverAsync(profile.Id, cancellationToken);
        var files = await _media.ListByOwnerAsync(profile.UserId, DriverKinds, cancellationToken);

        return Result.Success(new DriverReviewDetail(
            profile.Id, user.Id, user.FullName, user.Email, user.PhoneNumber,
            profile.TaxId, profile.CnhNumber, profile.CnhCategory,
            profile.Status, profile.RejectionReason, profile.ReviewedAt,
            vehicles.Select(v => new VehicleResponse(v.Id, v.Type, v.Plate, v.Model, v.Year, v.CapacityKg, v.IsActive)).ToList(),
            files.Select(f => new MediaFileResponse(f.Id, f.Kind, f.StorageKey, f.ContentType, f.SizeBytes, f.CreatedAt)).ToList()));
    }

    public async Task<Result> ApproveAsync(Guid reviewerId, Guid driverId, CancellationToken cancellationToken)
    {
        var loaded = await LoadPendingAsync(driverId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error!);
        }

        var (profile, user) = loaded.Value;

        // The file may have changed since it was submitted (vehicle removed, new term published).
        var vehicles = await _vehicles.ListActiveByDriverAsync(profile.Id, cancellationToken);
        var documents = await _media.ListByOwnerAsync(profile.UserId, new[] { MediaKind.DriverDocument }, cancellationToken);
        if (vehicles.Count == 0 || documents.Count == 0)
        {
            return Result.Failure(ReviewErrors.IncompleteFile);
        }

        profile.Approve(reviewerId, _clock.UtcNow);
        await _notifications.PublishAsync(NotificationMessages.DriverApproved(user.Id, profile.Id), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _email.SendAsync(
            user.Email,
            "Seu cadastro foi aprovado",
            $"Olá, {user.FullName}! Seu cadastro como motorista foi aprovado. Você já pode enviar propostas de frete.",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> RejectAsync(
        Guid reviewerId, Guid driverId, RejectDriverRequest request, CancellationToken cancellationToken)
    {
        var validation = await _rejectValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure(ValidationMapper.ToError(validation));
        }

        var loaded = await LoadPendingAsync(driverId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure(loaded.Error!);
        }

        var (profile, user) = loaded.Value;
        profile.Reject(reviewerId, request.Reason, _clock.UtcNow);
        await _notifications.PublishAsync(
            NotificationMessages.DriverRejected(user.Id, profile.Id, profile.RejectionReason), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _email.SendAsync(
            user.Email,
            "Seu cadastro precisa de ajustes",
            $"Olá, {user.FullName}. Não foi possível aprovar seu cadastro: {profile.RejectionReason} " +
            "Corrija as informações no aplicativo e envie novamente para análise.",
            cancellationToken);

        return Result.Success();
    }

    private async Task<Result<(DriverProfile Profile, User User)>> LoadPendingAsync(
        Guid driverId, CancellationToken cancellationToken)
    {
        var profile = await _drivers.GetByIdAsync(driverId, cancellationToken);
        var user = profile is null ? null : await _users.GetByIdAsync(profile.UserId, cancellationToken);
        if (profile is null || user is null)
        {
            return Result.Failure<(DriverProfile, User)>(ReviewErrors.DriverNotFound);
        }

        if (profile.Status != DriverStatus.PendingReview)
        {
            return Result.Failure<(DriverProfile, User)>(ReviewErrors.NotPendingReview);
        }

        return Result.Success((profile, user));
    }
}
