using Microsoft.Playwright;

namespace TankDesigner.Web.Services;

public class PdfRenderService
{
    public async Task<byte[]> GenerarPdfDesdeHtmlAsync(string html)
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();

        await page.SetContentAsync(html, new PageSetContentOptions
        {
            WaitUntil = WaitUntilState.Load
        });

        await page.EmulateMediaAsync(new PageEmulateMediaOptions
        {
            Media = Media.Screen
        });

        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true,
            Margin = new Margin
            {
                Top = "12mm",
                Bottom = "12mm",
                Left = "12mm",
                Right = "12mm"
            }
        });
    }
}