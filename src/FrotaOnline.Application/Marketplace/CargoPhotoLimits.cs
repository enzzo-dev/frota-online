namespace FrotaOnline.Application.Marketplace;

public static class CargoPhotoLimits
{
    public const int MaxSizeBytes = 2 * 1024 * 1024;

    public const int MaxBase64Length = MaxSizeBytes / 3 * 4 + 256;
}
