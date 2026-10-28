using FrotaOnline.Domain.Enums;

namespace FrotaOnline.Application.Deliveries;

public static class ContactPhone
{
    public static string? For(DeliveryStatus status, string? phoneNumber)
    {
        if (status == DeliveryStatus.Cancelled || string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        var phone = phoneNumber.Trim();
        return phone.Length < 10 ? null : phone;
    }
}
