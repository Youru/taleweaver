using System.Net;
using System.Net.Sockets;
using Taleweaver.Api.Hosting;

namespace Taleweaver.UnitTests.Features;

public class FreePortTests
{
    [Theory]
    [InlineData("0.0.0.0", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("::", true)] // Kestrel: another Taleweaver
    public void FreePort_SkipsAPortHeldOnAnyAddress(string address, bool dual)
    {
        var ip = IPAddress.Parse(address);
        using var holder = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        if (dual)
        {
            holder.DualMode = true;
        }

        holder.Bind(new IPEndPoint(ip, 0));
        holder.Listen();
        var held = ((IPEndPoint)holder.LocalEndPoint!).Port;

        PackagedApp.FreePort(held).Should().NotBe(held);
    }
}
