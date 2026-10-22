using FrotaOnline.Application.Abstractions.Routing;
using FrotaOnline.Application.Routing;

namespace FrotaOnline.Tests.Routing;

public class PolylineCodecTests
{
    private static readonly GeoPoint[] Reference =
    {
        new(38.5, -120.2),
        new(40.7, -120.95),
        new(43.252, -126.453),
    };

    [Fact]
    public void Encode_MatchesTheReferenceAlgorithm()
    {
        Assert.Equal("_p~iF~ps|U_ulLnnqC_mqNvxq`@", PolylineCodec.Encode(Reference));
    }

    [Fact]
    public void Decode_ReturnsThePointsInOrder()
    {
        var points = PolylineCodec.Decode("_p~iF~ps|U_ulLnnqC_mqNvxq`@");

        Assert.Equal(Reference.Length, points.Count);
        for (var i = 0; i < Reference.Length; i++)
        {
            Assert.Equal(Reference[i].Latitude, points[i].Latitude, 5);
            Assert.Equal(Reference[i].Longitude, points[i].Longitude, 5);
        }
    }

    [Fact]
    public void EmptyPath_RoundTrips()
    {
        Assert.Equal(string.Empty, PolylineCodec.Encode(Array.Empty<GeoPoint>()));
        Assert.Empty(PolylineCodec.Decode(string.Empty));
    }
}
