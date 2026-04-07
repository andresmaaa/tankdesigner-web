using Microsoft.Playwright;

namespace TankDesigner.Web.Services;

public class PdfRenderService
{
    public async Task<byte[]> GenerarPdfDesdeHtmlAsync(string html)
    {
        Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "/ms-playwright");

        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[]
            {
                "--no-sandbox",
                "--disable-setuid-sandbox"
            }
        });

        var page = await browser.NewPageAsync();

        await page.SetContentAsync(html);

        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true
        });
    }
}