using System.Net;
using Microsoft.AspNetCore.Http;
using Taleweaver.Api.Features.Rooms;

namespace Taleweaver.UnitTests.Features;

/// <summary>Who is the GM (room-spec §7): a guest writes the start of X-Forwarded-For, the proxy writes its end.</summary>
public class NetworkTests
{
    [Theory]
    [InlineData("127.0.0.1", null, true)]
    [InlineData("127.0.0.1", "127.0.0.1", true)]
    [InlineData("::1", "::1", true)]
    [InlineData("127.0.0.1", "::ffff:127.0.0.1", true)]
    [InlineData("127.0.0.1", "192.168.1.20", false)]
    [InlineData("127.0.0.1", "127.0.0.1,192.168.1.20", false)]
    [InlineData("127.0.0.1", "127.0.0.1, ::ffff:192.168.1.20", false)]
    [InlineData("127.0.0.1", "x,192.168.1.20", false)]
    [InlineData("127.0.0.1", "x", false)]
    [InlineData("127.0.0.1", "", false)]
    [InlineData("127.0.0.1", "127.0.0.1,", false)]
    [InlineData("192.168.1.20", "127.0.0.1", false)]
    public void OnlyTheAddressTheProxyAppended_MakesTheGm(string remote, string? forwarded, bool local)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        if (forwarded is not null)
        {
            context.Request.Headers["X-Forwarded-For"] = forwarded;
        }

        Network.IsLocal(context).Should().Be(local);
    }
}
