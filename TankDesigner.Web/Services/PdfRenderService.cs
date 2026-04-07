using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Net;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using Document = QuestPDF.Fluent.Document;

namespace TankDesigner.Web.Services;

public class PdfRenderService
{
    public Task<byte[]> GenerarPdfDesdeHtmlAsync(string html)
    {
        var titulo = ExtraerTitulo(html);
        var textoPlano = ConvertirHtmlATextoPlano(html);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12, Unit.Millimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header()
                    .Column(column =>
                    {
                        column.Spacing(4);
                        column.Item().Text(titulo)
                            .FontSize(18)
                            .SemiBold()
                            .FontColor(Colors.Blue.Medium);

                        column.Item().LineHorizontal(1);
                    });

                page.Content()
                    .PaddingVertical(6)
                    .Column(column =>
                    {
                        foreach (var bloque in SepararBloques(textoPlano))
                        {
                            column.Item().Text(bloque).LineHeight(1.25f);
                        }
                    });

                page.Footer()
                    .AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
            });
        }).GeneratePdf();

        return Task.FromResult(pdf);
    }

    private static string ExtraerTitulo(string html)
    {
        var match = Regex.Match(
            html ?? string.Empty,
            @"<title[^>]*>(.*?)</title>|<h1[^>]*>(.*?)</h1>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (match.Success)
        {
            var value = !string.IsNullOrWhiteSpace(match.Groups[1].Value)
                ? match.Groups[1].Value
                : match.Groups[2].Value;

            value = LimpiarTexto(value);

            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return "Informe";
    }

    private static IEnumerable<string> SepararBloques(string texto)
    {
        return texto
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static string ConvertirHtmlATextoPlano(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "Sin contenido disponible.";

        var text = html;

        text = Regex.Replace(text, @"<(script|style)[^>]*>.*?</\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        text = Regex.Replace(text, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</(p|div|section|article|header|footer|tr|table|h1|h2|h3|h4|h5|h6|ul|ol)>", "\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</li>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<li[^>]*>", "• ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<(td|th)[^>]*>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ", RegexOptions.Singleline);

        text = WebUtility.HtmlDecode(text);
        text = LimpiarTexto(text);

        return string.IsNullOrWhiteSpace(text) ? "Sin contenido disponible." : text;
    }

    private static string LimpiarTexto(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = text.Replace("\r", string.Empty);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");

        var lines = text
            .Split('\n')
            .Select(x => x.Trim())
            .ToArray();

        return string.Join("\n", lines).Trim();
    }
}