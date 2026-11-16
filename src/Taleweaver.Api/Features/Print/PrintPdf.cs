using System.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Taleweaver.Api.Common;

namespace Taleweaver.Api.Features.Print;

/// <summary>Where the browser that makes the PDFs is, and how long it may take.</summary>
public sealed class PrintOptions
{
    public const string Section = "Print";

    /// <summary>Edge or Chrome; empty → Edge, then Chrome, at their usual places.</summary>
    public string BrowserPath { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>Turns a page of the app into a PDF.</summary>
public interface IPdfPrinter
{
    Task<Result<byte[]>> PrintAsync(Uri page, CancellationToken cancellationToken);
}

/// <summary>
/// The workshop's print page as a finished PDF file: the browser downloads it whole, where "Save as
/// PDF" in the print dialog shows an empty file that fills up while it is written.
/// </summary>
public static class PrintPdf
{
    public sealed record Request(string? Scenario, string? Node, string? What, string? Veiled);

    public static class Errors
    {
        public static readonly Error NoScenario = Error.Validation("Print.NoScenario", "the scenario to print is missing");

        public static readonly Error NoPage = Error.Validation(
            "Print.NoPage", "the page asking for the PDF is not known (Referer): it must be the app, on this computer");

        public static Error NoBrowser(string where) => new("Print.NoBrowser", $"no Edge or Chrome found ({where}): set Print:BrowserPath", ErrorType.Failure);

        public static Error Failed(string why) => new("Print.Failed", $"the browser did not make the PDF: {why}", ErrorType.Failure);
    }

    public sealed class Handler(IPdfPrinter printer)
    {
        public async Task<Result<(byte[] Pdf, string FileName)>> Handle(Request request, Uri? from, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Scenario))
            {
                return Errors.NoScenario;
            }

            // The app itself, on this computer: never a page elsewhere
            if (from is null || !from.IsLoopback || from.Scheme is not ("http" or "https"))
            {
                return Errors.NoPage;
            }

            var query = new QueryBuilder(request);
            var page = new Uri(new Uri(from.GetLeftPart(UriPartial.Authority)), $"/workshop/print{query}");
            var pdf = await printer.PrintAsync(page, cancellationToken);
            return pdf.IsSuccess ? (pdf.Value, FileName(request)) : pdf.Error!;
        }

        /// <summary>"la-crypte-des-saules-marsh.pdf", "…-cards.pdf", "….pdf".</summary>
        public static string FileName(Request request)
        {
            var scenario = request.Scenario ?? "scenario";
            var name = scenario[(scenario.LastIndexOf('/') + 1)..];
            var part = request.What is { Length: > 0 } what ? $"-{what.TrimStart('@')}" : request.Node is { Length: > 0 } node ? $"-{node}" : "";
            var file = $"{name}{part}.pdf";
            return string.Concat(file.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c));
        }
    }

    private sealed class QueryBuilder(Request request)
    {
        public override string ToString()
        {
            var parts = new (string Key, string? Value)[]
            {
                ("scenario", request.Scenario), ("node", request.Node), ("what", request.What), ("veiled", request.Veiled),
            };
            return QueryString.Create(parts.Where(p => !string.IsNullOrEmpty(p.Value)).Select(p => new KeyValuePair<string, string?>(p.Key, p.Value))).ToString();
        }
    }

    public static IEndpointRouteBuilder MapPrintEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/print/pdf", async Task<Results<FileContentHttpResult, ProblemHttpResult>> (
                [AsParameters] Request request, HttpContext context, Handler handler, CancellationToken cancellationToken) =>
            {
                var referer = context.Request.Headers.Referer.ToString();
                var origin = context.Request.Headers.Origin.ToString();
                var from = Uri.TryCreate(referer, UriKind.Absolute, out var r) ? r : Uri.TryCreate(origin, UriKind.Absolute, out var o) ? o : null;
                var result = await handler.Handle(request, from, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.File(result.Value.Pdf, "application/pdf", result.Value.FileName)
                    : result.Error!.ToProblem();
            })
            .WithTags("Print")
            .WithName("PrintPdf");
        return app;
    }

    public static IServiceCollection AddPrintFeature(this IServiceCollection services)
    {
        services.AddOptions<PrintOptions>().BindConfiguration(PrintOptions.Section);
        services.AddSingleton<IPdfPrinter, BrowserPdfPrinter>();
        return services.AddSingleton<Handler>();
    }
}

/// <summary>
/// Edge (or Chrome) without a window: <c>--print-to-pdf</c> on the page, once its data and images are
/// in (<c>--virtual-time-budget</c>). The page sets its Letter size and backgrounds itself (CSS @page,
/// print-color-adjust), as for the print dialog.
/// </summary>
public sealed class BrowserPdfPrinter(IOptions<PrintOptions> options, ILogger<BrowserPdfPrinter> logger) : IPdfPrinter
{
    private static readonly string[] KnownPlaces =
    [
        @"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe",
        @"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe",
        @"%ProgramFiles%\Google\Chrome\Application\chrome.exe",
        @"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe",
        @"%LocalAppData%\Google\Chrome\Application\chrome.exe",
        "/usr/bin/microsoft-edge",
        "/usr/bin/google-chrome",
        "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
        "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    ];

    public async Task<Result<byte[]>> PrintAsync(Uri page, CancellationToken cancellationToken)
    {
        var configured = options.Value.BrowserPath;
        var browser = configured is { Length: > 0 }
            ? configured
            : KnownPlaces.Select(Environment.ExpandEnvironmentVariables).FirstOrDefault(File.Exists);
        if (browser is null || !File.Exists(browser))
        {
            return PrintPdf.Errors.NoBrowser(browser ?? "usual places");
        }

        // A profile and a file of its own: two PDFs at once never meet
        var work = Directory.CreateTempSubdirectory("taleweaver-print-");
        var output = Path.Combine(work.FullName, "page.pdf");
        try
        {
            var start = new ProcessStartInfo(browser) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var argument in new[]
            {
                "--headless", "--disable-gpu", "--no-first-run", "--no-default-browser-check", "--disable-extensions",
                "--no-pdf-header-footer", $"--user-data-dir={Path.Combine(work.FullName, "profile")}",
                "--virtual-time-budget=20000", $"--print-to-pdf={output}", page.AbsoluteUri,
            })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start)!;
            var errors = process.StandardError.ReadToEndAsync(cancellationToken);
            _ = process.StandardOutput.ReadToEndAsync(cancellationToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                return PrintPdf.Errors.Failed($"more than {options.Value.TimeoutSeconds} s");
            }

            if (!File.Exists(output) || new FileInfo(output).Length == 0)
            {
                var detail = (await errors).Trim();
                logger.LogWarning("No PDF for {Page}: {Errors}", page, detail);
                return PrintPdf.Errors.Failed($"no file (exit code {process.ExitCode})");
            }

            return await File.ReadAllBytesAsync(output, cancellationToken);
        }
        finally
        {
            try
            {
                work.Delete(recursive: true);
            }
            catch (IOException exception)
            {
                // The browser may still hold its profile a moment: the temp folder is cleaned later
                logger.LogDebug(exception, "Print folder left behind: {Folder}", work.FullName);
            }
        }
    }
}
