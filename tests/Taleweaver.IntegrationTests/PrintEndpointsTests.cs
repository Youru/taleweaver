using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Print;

namespace Taleweaver.IntegrationTests;

public class PrintEndpointsTests(HomebrewFactory factory) : IClassFixture<HomebrewFactory>
{
    private static readonly byte[] Pdf = "%PDF-1.4"u8.ToArray();

    private HttpClient Client()
    {
        var printer = Substitute.For<IPdfPrinter>();
        printer.PrintAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(Result<byte[]>.Success(Pdf));
        return factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(printer))).CreateClient();
    }

    [Fact]
    public async Task PrintPdf_FromTheApp_ReturnsTheFileToDownload()
    {
        var client = Client();
        client.DefaultRequestHeaders.Referrer = new Uri("http://localhost:5173/workshop/print?scenario=hb%3Ascenario%2Fduel");

        var response = await client.GetAsync("/api/print/pdf?scenario=hb:scenario/duel&what=cards", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.FileName.Should().Be("duel-cards.pdf");
        (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).Should().Equal(Pdf);
    }

    [Fact]
    public async Task PrintPdf_WithoutTheAppPage_ReturnsBadRequest()
    {
        var response = await Client().GetAsync("/api/print/pdf?scenario=hb:scenario/duel", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken))!.Title.Should().Be("Print.NoPage");
    }
}
