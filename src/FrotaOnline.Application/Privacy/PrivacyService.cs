using FluentValidation;
using FrotaOnline.Application.Abstractions.Auth;
using FrotaOnline.Application.Abstractions.Privacy;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Auth;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Privacy.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Privacy;

public sealed class PrivacyService : IPrivacyService
{
    // Told to the person so the erasure is transparent about what stays and why (LGPD art. 6, VI and art. 16).
    private static readonly string[] Retained =
    {
        "Freights, proposals and deliveries you took part in stay for audit and dispute handling, without your name, contact data or addresses.",
        "Payment and fee records stay for the period required by tax law, linked only to an anonymous account.",
        "The fact that you accepted the terms (which version and when) stays as proof, without your IP address.",
    };

    private readonly IUserRepository _users;
    private readonly IPersonalDataQueries _queries;
    private readonly IAccountErasureStore _erasure;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<DeleteAccountRequest> _validator;

    public PrivacyService(
        IUserRepository users,
        IPersonalDataQueries queries,
        IAccountErasureStore erasure,
        IPasswordHasher passwordHasher,
        IDateTimeProvider clock,
        IValidator<DeleteAccountRequest> validator)
    {
        _users = users;
        _queries = queries;
        _erasure = erasure;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _validator = validator;
    }

    public async Task<Result<PersonalDataExport>> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        var export = await _queries.GetAsync(userId, _clock.UtcNow, cancellationToken);
        return export is null
            ? Result.Failure<PersonalDataExport>(PrivacyErrors.AccountNotFound)
            : Result.Success(export);
    }

    public async Task<Result<AccountDeletionResponse>> DeleteAccountAsync(
        Guid userId, DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AccountDeletionResponse>(ValidationMapper.ToError(validation));
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null || user.IsAnonymized)
        {
            return Result.Failure<AccountDeletionResponse>(PrivacyErrors.AccountNotFound);
        }

        if (user.Role == UserRole.Admin)
        {
            return Result.Failure<AccountDeletionResponse>(PrivacyErrors.AdminNotAllowed);
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Result.Failure<AccountDeletionResponse>(AuthErrors.InvalidCredentials);
        }

        if (await _erasure.HasActiveDeliveriesAsync(userId, cancellationToken))
        {
            return Result.Failure<AccountDeletionResponse>(PrivacyErrors.ActiveDeliveries);
        }

        var now = _clock.UtcNow;
        await _erasure.EraseAsync(userId, now, cancellationToken);
        return Result.Success(new AccountDeletionResponse(now, Retained));
    }
}
