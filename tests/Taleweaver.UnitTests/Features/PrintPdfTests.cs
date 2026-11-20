using NSubstitute;
using Taleweaver.Api.Common;
using Taleweaver.Api.Features.Print;

namespace Taleweaver.UnitTests.Features;

public class PrintPdfTests
{
    private readonly IPdfPrinter _printer = Substitute.For<IPdfPrinter>();

    private PrintPdf.Handler Handler()
    {
        _printer.PrintAsync(Arg.Any<Uri>(), Arg.Any<CancellationToken>()).Returns(Result<byte[]>.Success([1, 2, 3]));
        return new PrintPdf.Handler(_printer);
    }

    [Fact]
    public async Task Handle_EncounterAskedFromTheApp_PrintsItsPrintPage()
    {
        var handler = Handler();

        var result = await handler.Handle(
            new PrintPdf.Request("homebrew:scenario/la-crypte", "marsh", null, "0"),
            new Uri("http://localhost:5173/workshop/print?scenario=x"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value.FileName.Should().Be("la-crypte-marsh.pdf");
        await _printer.Received(1).PrintAsync(
            new Uri("http://localhost:5173/workshop/print?scenario=homebrew%3Ascenario%2Fla-crypte&node=marsh&veiled=0"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AskedFromAnotherSite_IsRefused()
    {
        var handler = Handler();

        var result = await handler.Handle(new PrintPdf.Request("um1:scenario/blight", null, null, null), new Uri("http://example.com/"), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("Print.NoPage");
        await _printer.DidNotReceiveWithAnyArgs().PrintAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Handle_NoScenario_IsRefused()
    {
        var result = await Handler().Handle(new PrintPdf.Request(null, null, null, null), new Uri("http://localhost:5173/"), TestContext.Current.CancellationToken);

        result.Error!.Code.Should().Be("Print.NoScenario");
    }

    [Theory]
    [InlineData(null, null, "blight.pdf")]
    [InlineData("search", null, "blight-search.pdf")]
    [InlineData(null, "cards", "blight-cards.pdf")]
    public void FileName_WhatIsPrinted_NamesTheFile(string? node, string? what, string expected)
    {
        PrintPdf.Handler.FileName(new PrintPdf.Request("um1:scenario/blight", node, what, null)).Should().Be(expected);
    }

    [Fact]
    public async Task Handle_PartOfTheBooklet_PrintsThesePagesOnly()
    {
        var result = await Handler().Handle(
            new PrintPdf.Request("um1:scenario/blight", null, null, null, "5-12, 20"),
            new Uri("http://localhost:5173/workshop/print"),
            TestContext.Current.CancellationToken);

        result.Value.FileName.Should().Be("blight-p5-12_20.pdf");
        await _printer.Received(1).PrintAsync(
            Arg.Is<Uri>(u => System.Web.HttpUtility.ParseQueryString(u.Query)["pages"] == "5-12, 20"),
            Arg.Any<CancellationToken>());
    }
}
