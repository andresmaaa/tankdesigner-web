using System.Net;
using HtmlAgilityPack;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TankDesigner.Web.Services;

public class PdfRenderService
{
    public Task<byte[]> GenerarPdfDesdeHtmlAsync(string html)
    {
        var modelo = ParsearHtml(html);

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12, Unit.Millimetre);
                page.PageColor(Colors.White);

                page.DefaultTextStyle(x => x
                    .FontSize(10)
                    .FontColor(Colors.Grey.Darken4));

                page.Header()
                    .PaddingBottom(8)
                    .Column(column =>
                    {
                        column.Item().Text(modelo.Titulo)
                            .FontSize(18)
                            .SemiBold()
                            .FontColor(Colors.Blue.Darken2);

                        column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                page.Content()
                    .Column(column =>
                    {
                        foreach (var bloque in modelo.Bloques)
                        {
                            switch (bloque)
                            {
                                case PdfHeadingBlock heading:
                                    RenderHeading(column, heading);
                                    break;

                                case PdfParagraphBlock paragraph:
                                    RenderParagraph(column, paragraph);
                                    break;

                                case PdfTableBlock table:
                                    RenderTable(column, table);
                                    break;
                            }
                        }
                    });

                page.Footer()
                    .PaddingTop(8)
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

    private static void RenderHeading(ColumnDescriptor column, PdfHeadingBlock heading)
    {
        var size = heading.Level switch
        {
            1 => 18,
            2 => 15,
            3 => 13,
            _ => 12
        };

        column.Item()
            .PaddingTop(heading.Level == 1 ? 6 : 10)
            .PaddingBottom(4)
            .Text(heading.Text)
            .FontSize(size)
            .SemiBold()
            .FontColor(Colors.Blue.Darken2);
    }

    private static void RenderParagraph(ColumnDescriptor column, PdfParagraphBlock paragraph)
    {
        if (string.IsNullOrWhiteSpace(paragraph.Text))
            return;

        column.Item()
            .PaddingBottom(6)
            .Text(paragraph.Text)
            .LineHeight(1.3f);
    }

    private static void RenderTable(ColumnDescriptor column, PdfTableBlock table)
    {
        if (table.Headers.Count == 0 && table.Rows.Count == 0)
            return;

        column.Item()
            .PaddingTop(6)
            .PaddingBottom(10)
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(4)
            .Table(t =>
            {
                var totalColumns = table.Headers.Count > 0
                    ? table.Headers.Count
                    : table.Rows.Max(r => r.Count);

                t.ColumnsDefinition(cols =>
                {
                    for (int i = 0; i < totalColumns; i++)
                        cols.RelativeColumn();
                });

                if (table.Headers.Count > 0)
                {
                    t.Header(header =>
                    {
                        foreach (var cell in table.Headers)
                        {
                            header.Cell()
                                .Element(HeaderCellStyle)
                                .Text(LimpiarTexto(cell))
                                .SemiBold()
                                .FontSize(9);
                        }
                    });
                }

                foreach (var row in table.Rows)
                {
                    for (int i = 0; i < totalColumns; i++)
                    {
                        var value = i < row.Count ? row[i] : string.Empty;

                        t.Cell()
                            .Element(BodyCellStyle)
                            .Text(LimpiarTexto(value))
                            .FontSize(8.5f);
                    }
                }
            });
    }

    private static IContainer HeaderCellStyle(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Blue.Lighten5)
            .PaddingVertical(4)
            .PaddingHorizontal(5)
            .AlignMiddle();
    }

    private static IContainer BodyCellStyle(IContainer container)
    {
        return container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten3)
            .PaddingVertical(4)
            .PaddingHorizontal(5)
            .AlignMiddle();
    }

    private static PdfDocumentModel ParsearHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(string.IsNullOrWhiteSpace(html) ? "<html><body></body></html>" : html);

        var title =
            LimpiarTexto(doc.DocumentNode.SelectSingleNode("//title")?.InnerText) ??
            LimpiarTexto(doc.DocumentNode.SelectSingleNode("//h1")?.InnerText) ??
            "Informe";

        var modelo = new PdfDocumentModel
        {
            Titulo = string.IsNullOrWhiteSpace(title) ? "Informe" : title
        };

        var body = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;

        RecorrerNodos(body, modelo.Bloques);

        return modelo;
    }

    private static void RecorrerNodos(HtmlNode parent, List<PdfBlock> bloques)
    {
        foreach (var node in parent.ChildNodes)
        {
            if (node.NodeType == HtmlNodeType.Text)
                continue;

            var name = node.Name.ToLowerInvariant();

            switch (name)
            {
                case "h1":
                    AddHeading(node, bloques, 1);
                    break;

                case "h2":
                    AddHeading(node, bloques, 2);
                    break;

                case "h3":
                    AddHeading(node, bloques, 3);
                    break;

                case "p":
                case "div":
                case "section":
                case "article":
                case "header":
                case "footer":
                    AddParagraphIfDirectText(node, bloques);
                    RecorrerNodos(node, bloques);
                    break;

                case "ul":
                case "ol":
                    AddList(node, bloques);
                    break;

                case "table":
                    AddTable(node, bloques);
                    break;

                case "br":
                    break;

                default:
                    RecorrerNodos(node, bloques);
                    break;
            }
        }
    }

    private static void AddHeading(HtmlNode node, List<PdfBlock> bloques, int level)
    {
        var text = LimpiarTexto(node.InnerText);
        if (!string.IsNullOrWhiteSpace(text))
            bloques.Add(new PdfHeadingBlock { Level = level, Text = text });
    }

    private static void AddParagraphIfDirectText(HtmlNode node, List<PdfBlock> bloques)
    {
        var hasBlockChildren = node.ChildNodes.Any(x =>
            x.Name.Equals("table", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("div", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("section", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("article", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("p", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("ul", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("ol", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("h1", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("h2", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("h3", StringComparison.OrdinalIgnoreCase));

        if (!hasBlockChildren)
        {
            var text = LimpiarTexto(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
                bloques.Add(new PdfParagraphBlock { Text = text });
        }
    }

    private static void AddList(HtmlNode node, List<PdfBlock> bloques)
    {
        var items = node.SelectNodes("./li");
        if (items == null || items.Count == 0)
            return;

        foreach (var item in items)
        {
            var text = LimpiarTexto(item.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
                bloques.Add(new PdfParagraphBlock { Text = $"• {text}" });
        }
    }

    private static void AddTable(HtmlNode tableNode, List<PdfBlock> bloques)
    {
        var table = new PdfTableBlock();

        var headerRow =
            tableNode.SelectSingleNode(".//thead/tr") ??
            tableNode.SelectSingleNode(".//tr[th]");

        if (headerRow != null)
        {
            var headers = headerRow.SelectNodes("./th|./td");
            if (headers != null)
            {
                foreach (var cell in headers)
                    table.Headers.Add(LimpiarTexto(cell.InnerText));
            }
        }

        var rows = tableNode.SelectNodes(".//tr");
        if (rows != null)
        {
            foreach (var row in rows)
            {
                if (headerRow != null && row == headerRow)
                    continue;

                var cells = row.SelectNodes("./td|./th");
                if (cells == null || cells.Count == 0)
                    continue;

                var rowValues = cells
                    .Select(c => LimpiarTexto(c.InnerText))
                    .ToList();

                if (rowValues.All(string.IsNullOrWhiteSpace))
                    continue;

                table.Rows.Add(rowValues);
            }
        }

        if (table.Headers.Count > 0 || table.Rows.Count > 0)
            bloques.Add(table);
    }

    private static string LimpiarTexto(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        text = WebUtility.HtmlDecode(text);
        text = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");

        while (text.Contains("  "))
            text = text.Replace("  ", " ");

        return text.Trim();
    }

    private class PdfDocumentModel
    {
        public string Titulo { get; set; } = "Informe";
        public List<PdfBlock> Bloques { get; set; } = new();
    }

    private abstract class PdfBlock
    {
    }

    private class PdfHeadingBlock : PdfBlock
    {
        public int Level { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    private class PdfParagraphBlock : PdfBlock
    {
        public string Text { get; set; } = string.Empty;
    }

    private class PdfTableBlock : PdfBlock
    {
        public List<string> Headers { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }
}