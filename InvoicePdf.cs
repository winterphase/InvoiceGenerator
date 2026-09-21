using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CustomInvoiceApp;

public record InvoiceData(string Number, DateTime Date, Address Billing, Address Delivery, IReadOnlyList<LineItem> Lines);

public static class InvoicePdf
{
    static InvoicePdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;             // fonts installed on this computer
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false; // fall back to Lato if a font isn't installed
    }

    /// <param name="logoPath">Full path to the logo image (PNG, JPG or SVG), or null for none.</param>
    public static void Generate(string path, InvoiceData invoice, InvoiceProfile p, string? logoPath)
    {
        var subtotal = invoice.Lines.Sum(l => l.TotalExTax);
        var tax = invoice.Lines.Sum(l => l.Tax);
        var total = subtotal + tax;
        var hasLogo = logoPath != null && File.Exists(logoPath);
        var colour = ValidColour(p.TextColour);

        Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(t => t.FontSize(10).FontColor(colour).FontFamily(p.BodyFont, InvoiceProfile.FallbackFont));

            page.Content().Column(col =>
            {
                // Logo / business name + details (left), invoice title (right)
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        if (hasLogo) Logo(c.Item().MaxWidth((float)p.LogoWidth), logoPath!);
                        if (!hasLogo || p.ShowBusinessNameWithLogo)
                            c.Item().PaddingTop(hasLogo ? 6 : 0).Text(p.BusinessName)
                                .FontFamily(p.TitleFont, InvoiceProfile.FallbackFont).FontSize(18).Bold();
                        c.Item().PaddingTop(10).Column(info =>
                        {
                            foreach (var line in InvoiceProfile.SplitLines(p.BusinessDetails))
                                info.Item().Text(line).FontSize(9);
                        });
                    });

                    row.ConstantItem(180).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text(p.InvoiceTitle)
                            .FontFamily(p.TitleFont, InvoiceProfile.FallbackFont).FontSize(24).Bold();
                        c.Item().PaddingTop(4).AlignRight().Text($"Invoice no.: {invoice.Number}");
                        c.Item().AlignRight().Text($"Date: {invoice.Date:dd MMM yyyy}");
                    });
                });

                // Billing / delivery address boxes
                col.Item().PaddingTop(20).Row(row =>
                {
                    AddressBox(row.RelativeItem(), "Billing Address", invoice.Billing);
                    row.ConstantItem(16);
                    AddressBox(row.RelativeItem(), "Delivery Address", invoice.Delivery);
                });

                // Line items
                col.Item().PaddingTop(20).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(3.5f);
                        if (p.ShowUnitsColumn) c.RelativeColumn(1);
                        c.RelativeColumn(1);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });

                    table.Header(h =>
                    {
                        HeaderCell(h.Cell(), p.ItemLabel);
                        if (p.ShowUnitsColumn) HeaderCell(h.Cell(), p.UnitsLabel, right: true);
                        HeaderCell(h.Cell(), p.QuantityLabel, right: true);
                        HeaderCell(h.Cell(), p.PriceLabel, right: true);
                        HeaderCell(h.Cell(), p.TaxName, right: true);
                        HeaderCell(h.Cell(), p.TotalLabel, right: true);
                    });

                    foreach (var l in invoice.Lines)
                    {
                        BodyCell(table.Cell()).Text(l.Name);
                        if (p.ShowUnitsColumn) BodyCell(table.Cell()).AlignRight().Text($"{l.Units ?? 0:0}");
                        BodyCell(table.Cell()).AlignRight().Text($"{l.Quantity ?? 0:0}");
                        BodyCell(table.Cell()).AlignRight().Text(Money.Format(l.PriceExTax ?? 0));
                        BodyCell(table.Cell()).AlignRight().Text(Money.Format(l.Tax));
                        BodyCell(table.Cell()).AlignRight().Text(Money.Format(l.TotalIncTax));
                    }
                });

                // Totals
                col.Item().PaddingTop(16).AlignRight().Width(220).PreventPageBreak().Column(t =>
                {
                    TotalRow(t, $"Subtotal ex {p.TaxName}", subtotal);
                    TotalRow(t, $"{p.TaxName} ({p.TaxRatePercent:0.##}%)", tax);
                    t.Item().PaddingTop(4).BorderTop(1).PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text(p.TotalLabel).Bold();
                        r.RelativeItem().AlignRight().Text(Money.Format(total)).Bold();
                    });
                });

                // Payment details + signature, pushed to the bottom of the last page
                if (InvoiceProfile.SplitLines(p.PaymentDetails).Length > 0 || p.ShowSignatureLines)
                    col.Item().ExtendVertical().AlignBottom().PaddingTop(24).PreventPageBreak()
                        .Element(c => PaymentAndSignature(c, p, colour));
            });

            page.Footer().PaddingTop(12).AlignCenter().Column(c =>
            {
                c.Item().AlignCenter().DefaultTextStyle(x => x.FontSize(8)).Text(t => t.CurrentPageNumber());
                foreach (var line in InvoiceProfile.SplitLines(p.SmallPrint))
                    c.Item().AlignCenter().Text(line).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                if (!string.IsNullOrWhiteSpace(p.Website))
                {
                    var site = p.Website.Trim();
                    var url = site.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? site : "https://" + site;
                    c.Item().AlignCenter().Hyperlink(url)
                        .Text(site).FontSize(7.5f).FontColor(Colors.Grey.Darken1).Underline();
                }
            });
        })).GeneratePdf(path);
    }

    private static void Logo(IContainer container, string path)
    {
        if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            container.Svg(InlineSvgClasses(File.ReadAllText(path)));
        else
            container.Image(path).FitWidth();
    }

    /// <summary>
    /// SVGs exported from Illustrator etc. often colour shapes with CSS classes (.st0 { fill: #333 }),
    /// which the PDF renderer ignores — swap them for plain fill attributes.
    /// </summary>
    private static string InlineSvgClasses(string svg)
    {
        foreach (Match m in Regex.Matches(svg, @"\.([\w-]+)\s*\{\s*fill:\s*([^;}\s]+)\s*;?\s*\}"))
            svg = svg.Replace($"class=\"{m.Groups[1].Value}\"", $"fill=\"{m.Groups[2].Value}\"");
        return svg;
    }

    public static bool IsValidColour(string? colour) =>
        !string.IsNullOrWhiteSpace(colour) && Regex.IsMatch(colour.Trim(), "^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$");

    private static string ValidColour(string colour) => IsValidColour(colour) ? colour.Trim() : "#333333";

    private static void AddressBox(IContainer container, string title, Address address)
    {
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).MinHeight(100).Column(c =>
        {
            c.Item().PaddingBottom(4).Text(title).Bold();
            if (!string.IsNullOrWhiteSpace(address.CustomerName))
                c.Item().Text(address.CustomerName.Trim());
            foreach (var line in address.Lines)
                c.Item().Text(line);
        });
    }

    /// <summary>Payment lines written as "Label: value" get a bold label; other lines print as-is.</summary>
    private static void PaymentAndSignature(IContainer container, InvoiceProfile p, string colour)
    {
        container.BorderTop(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingTop(12).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                foreach (var line in InvoiceProfile.SplitLines(p.PaymentDetails))
                {
                    var i = line.IndexOf(':');
                    if (i > 0)
                        c.Item().PaddingBottom(3).Row(r =>
                        {
                            r.ConstantItem(95).Text(line[..i]).Bold();
                            r.RelativeItem().Text(line[(i + 1)..].Trim());
                        });
                    else
                        c.Item().PaddingBottom(3).Text(line);
                }
            });

            if (p.ShowSignatureLines)
                row.RelativeItem().PaddingLeft(24).Column(c =>
                {
                    SignatureLine(c, "Name", colour);
                    SignatureLine(c, "Date", colour);
                });
        });
    }

    private static void SignatureLine(ColumnDescriptor col, string label, string colour) =>
        col.Item().PaddingBottom(12).Row(r =>
        {
            r.ConstantItem(40).AlignBottom().Text(label).Bold();
            r.RelativeItem().Height(18).BorderBottom(0.75f).BorderColor(colour);
        });

    private static void HeaderCell(IContainer cell, string text, bool right = false)
    {
        var c = cell.Background(Colors.Grey.Lighten3).PaddingVertical(6).PaddingHorizontal(4);
        (right ? c.AlignRight() : c).Text(text).Bold();
    }

    private static IContainer BodyCell(IContainer cell) =>
        cell.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5).PaddingHorizontal(4);

    private static void TotalRow(ColumnDescriptor col, string label, decimal amount) =>
        col.Item().Row(r =>
        {
            r.RelativeItem().Text(label);
            r.RelativeItem().AlignRight().Text(Money.Format(amount));
        });
}
