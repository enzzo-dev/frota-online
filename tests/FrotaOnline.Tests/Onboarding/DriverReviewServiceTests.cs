using FrotaOnline.Application.Admin;
using FrotaOnline.Tests.Marketplace;
using FrotaOnline.Application.Admin.Dtos;
using FrotaOnline.Application.Admin.Validators;
using FrotaOnline.Application.Common;
using FrotaOnline.Application.Notifications;
using FrotaOnline.Tests.Notifications;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;
using FrotaOnline.Tests.Auth;

namespace FrotaOnline.Tests.Onboarding;

public class DriverReviewServiceTests
{
    private readonly FakeDriverRepository _drivers = new();
    private readonly FakeUserRepository _users = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeClock _clock = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeVehicleRepository _vehicles = new();
    private readonly FakeMediaFileRepository _media = new();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly DriverReviewService _sut;

    public DriverReviewServiceTests()
    {
        _sut = new DriverReviewService(
            _drivers, new FakeDriverReviewQueries(_drivers, _users), _users, _vehicles, _media, _email,
            new NotificationPublisher(_notifications), new FakeUnitOfWork(), _clock, new RejectDriverRequestValidator());
    }

    private DriverProfile AddDriver(bool pending = true)
    {
        var user = User.Create("Joao Silva", "joao@example.com", "+5511999999999", "hash", UserRole.Driver);
        _users.Items.Add(user);
        var profile = DriverProfile.Create(user.Id, "52998224725", "12345678901", "B");
        if (pending)
        {
            profile.SubmitForReview();
        }

        _drivers.Items.Add(profile);
        _vehicles.Items.Add(Vehicle.Create(profile.Id, VehicleType.Fiorino, "ABC1D23", "Fiorino", 2020, 600));
        _media.Items.Add(MediaFile.Create(user.Id, MediaKind.DriverDocument, $"drivers/{user.Id}/cnh.jpg", "image/jpeg", 100));
        return profile;
    }

    [Fact]
    public async Task List_ReturnsPendingDriversWithContactData()
    {
        AddDriver();
        AddDriver(pending: false);

        var result = await _sut.ListAsync(DriverStatus.PendingReview, CancellationToken.None);

        var item = Assert.Single(result.Value);
        Assert.Equal("Joao Silva", item.FullName);
    }

    [Fact]
    public async Task Approve_ChangesStatusAndSendsEmail()
    {
        var profile = AddDriver();

        var result = await _sut.ApproveAsync(_adminId, profile.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DriverStatus.Approved, profile.Status);
        Assert.Equal(_adminId, profile.ReviewedByUserId);
        Assert.Equal("joao@example.com", Assert.Single(_email.Sent).To);
    }

    [Fact]
    public async Task Approve_NotifiesTheDriverInTheApp()
    {
        var profile = AddDriver();

        await _sut.ApproveAsync(_adminId, profile.Id, CancellationToken.None);

        var notification = Assert.Single(_notifications.Items);
        Assert.Equal(NotificationType.DriverApproved, notification.Type);
        Assert.Equal(profile.UserId, notification.UserId);
    }

    [Fact]
    public async Task Reject_NotifiesTheDriverWithTheReason()
    {
        var profile = AddDriver();

        await _sut.RejectAsync(_adminId, profile.Id, new RejectDriverRequest("CNH ilegível"), CancellationToken.None);

        var notification = Assert.Single(_notifications.Items);
        Assert.Equal(NotificationType.DriverRejected, notification.Type);
        Assert.Contains("CNH ilegível", notification.Body);
    }

    [Fact]
    public async Task Approve_DriverNotPending_ReturnsConflict_AndSendsNothing()
    {
        var profile = AddDriver(pending: false);

        var result = await _sut.ApproveAsync(_adminId, profile.Id, CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Empty(_email.Sent);
    }

    [Fact]
    public async Task Approve_WithoutActiveVehicle_ReturnsConflict()
    {
        var profile = AddDriver();
        _vehicles.Items.Clear();

        var result = await _sut.ApproveAsync(_adminId, profile.Id, CancellationToken.None);

        Assert.Equal("admin.incomplete_file", result.Error!.Code);
        Assert.Equal(DriverStatus.PendingReview, profile.Status);
    }

    [Fact]
    public async Task Approve_UnknownDriver_ReturnsNotFound()
    {
        var result = await _sut.ApproveAsync(_adminId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Reject_StoresReasonAndEmailsIt()
    {
        var profile = AddDriver();

        var result = await _sut.RejectAsync(_adminId, profile.Id, new RejectDriverRequest("CNH ilegível"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DriverStatus.Rejected, profile.Status);
        Assert.Equal("CNH ilegível", profile.RejectionReason);
        Assert.Contains("CNH ilegível", Assert.Single(_email.Sent).Body);
    }

    [Fact]
    public async Task Reject_WithoutReason_IsRejectedByValidation()
    {
        var profile = AddDriver();

        var result = await _sut.RejectAsync(_adminId, profile.Id, new RejectDriverRequest(" "), CancellationToken.None);

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(DriverStatus.PendingReview, profile.Status);
    }

    [Fact]
    public async Task RejectedDriver_CanEditAndResubmit()
    {
        var profile = AddDriver();
        await _sut.RejectAsync(_adminId, profile.Id, new RejectDriverRequest("Foto ruim"), CancellationToken.None);

        profile.UpdateDetails("52998224725", "10987654321", "C");
        profile.SubmitForReview();

        Assert.Equal(DriverStatus.PendingReview, profile.Status);
        Assert.Null(profile.RejectionReason);
    }
}
