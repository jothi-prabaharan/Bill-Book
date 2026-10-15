using System.Globalization;
using Microsoft.Extensions.Logging;
using Printing.Entity.Enums;
using Printing.Entity.Models;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace Printing.Repository.Repositories;

/// <summary>
/// Server-side PDF rendering using PuppeteerSharp and headless Chromium/Edge.
/// Produces exact vector PDF matching the template HTML, CSS, fonts, SVG barcodes,
/// QR codes, and page dimensions with 100% fidelity.
/// </summary>
public sealed class PrintPdfGeneratorRepository : IAsyncDisposable
{
    private readonly ILogger<PrintPdfGeneratorRepository>? _logger;
    private IBrowser? _browser;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isDisposed;

    public PrintPdfGeneratorRepository(ILogger<PrintPdfGeneratorRepository>? logger = null)
    {
        _logger = logger;
    }

    private async Task<IBrowser> EnsureBrowserAsync()
    {
        if (_browser is not null && !_browser.IsClosed)
        {
            return _browser;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_browser is not null && !_browser.IsClosed)
            {
                return _browser;
            }

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args =
                [
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-gpu",
                    "--disable-dev-shm-usage",
                    "--font-render-hinting=medium",
                ],
            };

            string? browserPath = FindInstalledBrowser();
            if (!string.IsNullOrEmpty(browserPath))
            {
                launchOptions.ExecutablePath = browserPath;
                _logger?.LogInformation("Using installed browser for PDF generation at {Path}", browserPath);
            }
            else
            {
                _logger?.LogInformation("No installed Chromium found; downloading revision via BrowserFetcher...");
                var fetcher = new BrowserFetcher();
                var installed = await fetcher.DownloadAsync();
                launchOptions.ExecutablePath = installed.GetExecutablePath();
                _logger?.LogInformation("Downloaded Chromium at {Path}", launchOptions.ExecutablePath);
            }

            _browser = await Puppeteer.LaunchAsync(launchOptions);
            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static string? FindInstalledBrowser()
    {
        // 1. Environment variable override
        string? envPath = Environment.GetEnvironmentVariable("CHROME_PATH")
            ?? Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH");
        if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        // 2. Windows Microsoft Edge / Chrome
        string[] candidates =
        [
            @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
            "/usr/bin/google-chrome",
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser",
            "/usr/bin/microsoft-edge",
        ];

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Renders HTML to a high-fidelity PDF byte array according to paper settings.
    /// </summary>
    public async Task<byte[]> RenderHtmlToPdfAsync(string html, PrintSettings settings, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(html);
        ArgumentNullException.ThrowIfNull(settings);

        IBrowser browser = await EnsureBrowserAsync();
        await using IPage page = await browser.NewPageAsync();

        // Wrap HTML in a full HTML5 document if not already wrapped
        string fullHtml = html.Contains("<html", StringComparison.OrdinalIgnoreCase)
            ? html
            : $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Print Document</title><style>body {{ margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; }}</style></head><body>{html}</body></html>";

        await page.SetContentAsync(fullHtml, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.Load, WaitUntilNavigation.Networkidle0],
            Timeout = 30000,
        });

        PdfOptions pdfOptions = await BuildPdfOptionsAsync(page, settings);
        return await page.PdfDataAsync(pdfOptions);
    }

    private static async Task<PdfOptions> BuildPdfOptionsAsync(IPage page, PrintSettings settings)
    {
        if (settings.PrinterType == PrinterType.Thermal)
        {
            // Continuous roll: measure document height in pixels and translate to mm
            int scrollHeightPx = await page.EvaluateExpressionAsync<int>(
                "Math.max(document.documentElement.scrollHeight || 0, document.body.scrollHeight || 0, 100)");

            double rollWidthMm = settings.RollWidthMm > 0 ? settings.RollWidthMm : 80d;
            // 96 CSS pixels per inch = 25.4 mm per inch
            double heightMm = Math.Max(40d, (scrollHeightPx * 25.4d / 96.0d) + 8d);

            return new PdfOptions
            {
                Width = string.Create(CultureInfo.InvariantCulture, $"{rollWidthMm:F1}mm"),
                Height = string.Create(CultureInfo.InvariantCulture, $"{heightMm:F1}mm"),
                PrintBackground = true,
                MarginOptions = new MarginOptions
                {
                    Top = string.Create(CultureInfo.InvariantCulture, $"{settings.MarginTopMm:F1}mm"),
                    Bottom = string.Create(CultureInfo.InvariantCulture, $"{settings.MarginBottomMm:F1}mm"),
                    Left = string.Create(CultureInfo.InvariantCulture, $"{settings.MarginLeftMm:F1}mm"),
                    Right = string.Create(CultureInfo.InvariantCulture, $"{settings.MarginRightMm:F1}mm"),
                },
                PreferCSSPageSize = false,
            };
        }

        PaperFormat format = settings.PaperSize switch
        {
            PaperSize.A5 => PaperFormat.A5,
            PaperSize.Letter => PaperFormat.Letter,
            PaperSize.Legal => PaperFormat.Legal,
            _ => PaperFormat.A4,
        };

        return new PdfOptions
        {
            Format = format,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = string.Create(CultureInfo.InvariantCulture, $"{settings.MarginTopMm:F1}mm"),
                Bottom = string.Create(CultureInfo.InvariantCulture, $"{settings.MarginBottomMm:F1}mm"),
                Left = string.Create(CultureInfo.InvariantCulture, $"{settings.MarginLeftMm:F1}mm"),
                Right = string.Create(CultureInfo.InvariantCulture, $"{settings.MarginRightMm:F1}mm"),
            },
            PreferCSSPageSize = true,
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_browser is not null)
        {
            try
            {
                await _browser.CloseAsync();
                _browser.Dispose();
            }
            catch
            {
                // Ignore disposal errors on shutdown
            }
        }

        _initLock.Dispose();
    }
}
