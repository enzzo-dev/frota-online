using FrotaOnline.Application.Notifications;
using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Tests.Notifications;

public class NotificationMessagesTests
{
    [Theory]
    [InlineData("150.00", "R$ 150,00")]
    [InlineData("1234.5", "R$ 1.234,50")]
    [InlineData("1234567.89", "R$ 1.234.567,89")]
    public void Money_IsFormattedTheBrazilianWay_WhateverTheServerCulture(string price, string expected)
    {
        var message = NotificationMessages.ProposalReceived(
            Guid.NewGuid(), Guid.NewGuid(), decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Contains(expected, message.Body);
    }

    [Fact]
    public void LongFreeText_IsCutSoThePushStaysReadable()
    {
        var message = NotificationMessages.DeliveryIncident(Guid.NewGuid(), Guid.NewGuid(), new string('x', 500));

        Assert.True(message.Body.Length < 220);
        Assert.EndsWith("…", message.Body);
    }

    [Fact]
    public void MissingReason_StillProducesAReadableBody()
    {
        var message = NotificationMessages.DriverRejected(Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.Contains("sem detalhes", message.Body);
    }

    [Fact]
    public void DeliveryMessages_PointToTheDelivery()
    {
        var deliveryId = Guid.NewGuid();

        var message = NotificationMessages.DeliveryCompleted(Guid.NewGuid(), deliveryId);

        Assert.Equal(NotificationTarget.Delivery, message.TargetType);
        Assert.Equal(deliveryId, message.TargetId);
    }
}
