using FluentValidation;
using FrotaOnline.Application.Abstractions.Auth;
using FrotaOnline.Application.Abstractions.Payments;
using FrotaOnline.Application.Abstractions.Privacy;
using FrotaOnline.Application.Abstractions.Storage;
using FrotaOnline.Application.Abstractions.Time;
using FrotaOnline.Application.Abstractions.Users;
using FrotaOnline.Application.Auth;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Payments;
using FrotaOnline.Application.Privacy.Dtos;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Privacy;

public sealed class PrivacyService : IPrivacyService
{
    // Told to the person so the erasure is transparent about what stays and why (LGPD art. 6, VI and art. 16).
    private static readonly string[] Retained =
    {
        "Os fretes, propostas e entregas de que você participou permanecem para auditoria e tratamento de disputas, sem seu nome, dados de contato ou endereços.",
        "Os registros de pagamentos e taxas permanecem pelo prazo exigido pela legislação fiscal, vinculados apenas a uma conta anônima.",
        "O fato de você ter aceitado os termos (qual versão e quando) permanece como comprovação, sem o seu endereço de IP.",
    };

    private readonly IUserRepository _users;
    private readonly IPersonalDataQueries _queries;
    private readonly IAccountErasureStore _erasure;
    private readonly IFileStorage _storage;
    private readonly IPaymentGateway _gateway;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _clock;
    private readonly IValidator<DeleteAccountRequest> _validator;

    public PrivacyService(
        IUserRepository users,
        IPersonalDataQueries queries,
        IAccountErasureStore erasure,
        IFileStorage storage,
        IPaymentGateway gateway,
        IPasswordHasher passwordHasher,
        IDateTimeProvider clock,
        IValidator<DeleteAccountRequest> validator)
    {
        _users = users;
        _queries = queries;
        _erasure = erasure;
        _storage = storage;
        _gateway = gateway;
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

        // A subscription left running would keep charging a person who no longer exists: stop it first, and if the
        // provider cannot be reached, keep the account so the person can try again.
        if (_gateway.IsConfigured)
        {
            try
            {
                foreach (var subscriptionId in await _erasure.ListOpenSubscriptionIdsAsync(userId, cancellationToken))
                {
                    await _gateway.CancelSubscriptionAsync(subscriptionId, cancellationToken);
                }
            }
            catch (PaymentGatewayException)
            {
                return Result.Failure<AccountDeletionResponse>(PaymentErrors.GatewayUnavailable);
            }
        }

        var now = _clock.UtcNow;
        var storageKeys = await _erasure.EraseAsync(userId, now, cancellationToken);

        // The database is already clean; the objects go next. The storage never throws, so a failure here leaves
        // orphan files to be cleaned by a bucket lifecycle rule, but never undoes the erasure.
        if (storageKeys.Count > 0 && _storage.IsConfigured)
        {
            await _storage.DeleteAsync(storageKeys, cancellationToken);
        }

        return Result.Success(new AccountDeletionResponse(now, Retained));
    }
}
