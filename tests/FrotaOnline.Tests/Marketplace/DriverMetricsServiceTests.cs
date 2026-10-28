using FrotaOnline.Application.Common;
using FrotaOnline.Application.Marketplace;
using FrotaOnline.Domain.Entities;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Marketplace;

public class DriverMetricsServiceTests : MarketplaceTestBase
{
    private readonly FakeFreightViewRepository _views = new();
    private readonly FakeDriverMetricsQueries _queries = new();

    private DriverMetricsService Service => new(Drivers, Freights, _views, _queries, Clock);

    [Fact]
    public async Task View_IsRecordedOncePerDriverAndFreight_AndUpdatesTheLastView()
    {
        var freightId = await CreateFreightAsync();
        var (userId, profile, _) = AddApprovedDriver();

        await Service.RecordFreightViewAsync(userId, freightId, CancellationToken.None);
        var first = Clock.UtcNow;
        Clock.Advance(TimeSpan.FromMinutes(1));
        var result = await Service.RecordFreightViewAsync(userId, freightId, CancellationToken.None);

        var view = Assert.Single(_views.Items);
        Assert.True(result.IsSuccess);
        Assert.Equal(profile.Id, view.DriverProfileId);
        Assert.Equal(2, view.ViewCount);
        Assert.Equal(first, view.FirstViewedAt);
        Assert.Equal(Clock.UtcNow, view.LastViewedAt);
    }

    [Fact]
    public async Task View_ByDriverNotApproved_ReturnsForbidden()
    {
        var freightId = await CreateFreightAsync();
        var userId = Guid.NewGuid();
        Drivers.Items.Add(DriverProfile.Create(userId, "52998224701", "12345678901", "B"));

        var result = await Service.RecordFreightViewAsync(userId, freightId, CancellationToken.None);

        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Empty(_views.Items);
    }

    [Fact]
    public async Task View_OfUnknownFreight_ReturnsNotFound()
    {
        var (userId, _, _) = AddApprovedDriver();

        var result = await Service.RecordFreightViewAsync(userId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task View_OfAFreightThatIsNotOpen_SucceedsWithoutRecording()
    {
        var freightId = await CreateFreightAsync();
        await FreightService.CancelAsync(CustomerId, freightId, CancellationToken.None);
        var (userId, _, _) = AddApprovedDriver();

        var result = await Service.RecordFreightViewAsync(userId, freightId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_views.Items);
    }

    [Fact]
    public async Task Metrics_AreReadForTheDriversProfile_WithThePeriodInUtc()
    {
        var (userId, profile, _) = AddApprovedDriver();
        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 31, 0, 0, 0, DateTimeKind.Utc);

        var result = await Service.GetAsync(userId, from, to, CancellationToken.None);

        Assert.Equal(5, result.Value.FreightsSeen);
        Assert.Equal((profile.Id, (DateTime?)from, (DateTime?)to), _queries.LastCall);
    }

    [Fact]
    public async Task Metrics_WithTheStartAfterTheEnd_IsRejected()
    {
        var (userId, _, _) = AddApprovedDriver();

        var result = await Service.GetAsync(
            userId, new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        Assert.Equal("metrics.invalid_period", result.Error!.Code);
    }

    [Fact]
    public async Task Metrics_ByUserWithoutDriverProfile_ReturnsNotFound()
    {
        var result = await Service.GetAsync(Guid.NewGuid(), null, null, CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }
}
