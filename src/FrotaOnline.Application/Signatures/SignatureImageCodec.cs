using System.IO.Compression;

namespace FrotaOnline.Application.Signatures;

public static class SignatureImageCodec
{
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF };

    public static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        var payload = value.Trim();
        if (payload.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = payload.IndexOf(',');
            payload = comma < 0 ? string.Empty : payload[(comma + 1)..];
        }

        bytes = new byte[payload.Length * 3 / 4 + 3];
        if (!Convert.TryFromBase64String(payload, bytes, out var written))
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        bytes = bytes[..written];
        return true;
    }

    public static string? DetectContentType(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(PngHeader))
        {
            return "image/png";
        }

        return bytes.AsSpan().StartsWith(JpegHeader) ? "image/jpeg" : null;
    }

    public static byte[] Compress(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            brotli.Write(bytes);
        }

        return output.ToArray();
    }

    public static byte[] Decompress(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }
}
