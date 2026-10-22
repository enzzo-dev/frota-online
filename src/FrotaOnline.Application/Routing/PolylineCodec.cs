using System.Text;
using FrotaOnline.Application.Abstractions.Routing;

namespace FrotaOnline.Application.Routing;

public static class PolylineCodec
{
    private const double Factor = 1e5;

    public static string Encode(IReadOnlyList<GeoPoint> points)
    {
        var builder = new StringBuilder(points.Count * 8);
        long previousLatitude = 0;
        long previousLongitude = 0;

        foreach (var point in points)
        {
            var latitude = (long)Math.Round(point.Latitude * Factor);
            var longitude = (long)Math.Round(point.Longitude * Factor);
            Write(builder, latitude - previousLatitude);
            Write(builder, longitude - previousLongitude);
            previousLatitude = latitude;
            previousLongitude = longitude;
        }

        return builder.ToString();
    }

    public static IReadOnlyList<GeoPoint> Decode(string polyline)
    {
        var points = new List<GeoPoint>();
        var index = 0;
        long latitude = 0;
        long longitude = 0;

        while (index < polyline.Length)
        {
            latitude += Read(polyline, ref index);
            longitude += Read(polyline, ref index);
            points.Add(new GeoPoint(latitude / Factor, longitude / Factor));
        }

        return points;
    }

    private static void Write(StringBuilder builder, long value)
    {
        var remaining = value < 0 ? ~(value << 1) : value << 1;
        while (remaining >= 0x20)
        {
            builder.Append((char)((0x20 | (remaining & 0x1f)) + 63));
            remaining >>= 5;
        }

        builder.Append((char)(remaining + 63));
    }

    private static long Read(string polyline, ref int index)
    {
        long result = 0;
        var shift = 0;
        long chunk;
        do
        {
            chunk = polyline[index++] - 63;
            result |= (chunk & 0x1f) << shift;
            shift += 5;
        }
        while (chunk >= 0x20);

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }
}
