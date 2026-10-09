using FrotaOnline.Application.Common;
using FrotaOnline.Application.Terms;
using FrotaOnline.Application.Terms.Dtos;
using FrotaOnline.Application.Terms.Validators;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;

namespace FrotaOnline.Tests.Onboarding;

public class TermServiceTests
{
    private readonly FakeTermRepository _terms = new();
    private readonly FakeConsentRepository _consents = new();
    private readonly FakeClock _clock = new();
    private readonly TermService _sut;

    public TermServiceTests()
    {
        _sut = new TermService(
            _terms, _consents, new FakeUnitOfWork(), _clock,
            new AcceptTermRequestValidator(), new PublishTermRequestValidator());
    }

    private TermDocument AddTerm(TermType type = TermType.TermsOfUse, string version = "1.0")
    {
        var term = TermDocument.Create(type, version, "Title", "https://example.com/t", _clock.UtcNow);
        _terms.Items.Add(term);
        return term;
    }

    [Fact]
    public async Task Pending_ListsOnlyActiveTermsNotYetAccepted()
    {
        var userId = Guid.NewGuid();
        var terms = AddTerm();
        AddTerm(TermType.PrivacyPolicy);
        await _sut.AcceptAsync(userId, new AcceptTermRequest(terms.Id), "127.0.0.1", CancellationToken.None);

        var result = await _sut.GetPendingAsync(userId, CancellationToken.None);

        Assert.Single(result.Value);
        Assert.Equal(TermType.PrivacyPolicy, result.Value[0].Type);
    }

    [Fact]
    public async Task Accept_RecordsConsentWithIp()
    {
        var term = AddTerm();

        var result = await _sut.AcceptAsync(Guid.NewGuid(), new AcceptTermRequest(term.Id), "10.0.0.1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("10.0.0.1", Assert.Single(_consents.Items).IpAddress);
    }

    [Fact]
    public async Task Accept_Twice_IsIdempotent()
    {
        var userId = Guid.NewGuid();
        var term = AddTerm();

        await _sut.AcceptAsync(userId, new AcceptTermRequest(term.Id), null, CancellationToken.None);
        var second = await _sut.AcceptAsync(userId, new AcceptTermRequest(term.Id), null, CancellationToken.None);

        Assert.True(second.IsSuccess);
        Assert.Single(_consents.Items);
    }

    [Fact]
    public async Task Accept_UnknownTerm_ReturnsNotFound()
    {
        var result = await _sut.AcceptAsync(Guid.NewGuid(), new AcceptTermRequest(Guid.NewGuid()), null, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Accept_InactiveVersion_ReturnsConflict()
    {
        var term = AddTerm();
        term.Deactivate();

        var result = await _sut.AcceptAsync(Guid.NewGuid(), new AcceptTermRequest(term.Id), null, CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task Publish_DeactivatesPreviousVersionOfSameType()
    {
        var old = AddTerm(TermType.TermsOfUse, "1.0");
        var other = AddTerm(TermType.PrivacyPolicy, "1.0");

        var result = await _sut.PublishAsync(
            new PublishTermRequest(TermType.TermsOfUse, "2.0", "New terms", "https://example.com/v2"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(old.IsActive);
        Assert.True(other.IsActive);
        Assert.Equal(3, _terms.Items.Count);
    }

    [Fact]
    public async Task Publish_SameVersionTwice_ReturnsConflict()
    {
        AddTerm(TermType.TermsOfUse, "1.0");

        var result = await _sut.PublishAsync(
            new PublishTermRequest(TermType.TermsOfUse, "1.0", "Dup", "https://example.com/dup"), CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task Publish_WithHttpUrl_IsRejectedByValidation()
    {
        var result = await _sut.PublishAsync(
            new PublishTermRequest(TermType.TermsOfUse, "3.0", "Insecure", "http://example.com/t"), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }
}
