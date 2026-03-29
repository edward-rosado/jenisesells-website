using System.Globalization;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Activities.Pdf;

public class CmaPdfGenerator : ICmaPdfGenerator
{
    private static readonly CultureInfo EnUs = new("en-US");

    private readonly ILogger<CmaPdfGenerator> _logger;

    static CmaPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public CmaPdfGenerator(ILogger<CmaPdfGenerator> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateAsync(
        Lead lead,
        CmaAnalysis analysis,
        List<Comp> comps,
        AccountConfig config,
        ReportType reportType,
        byte[]? logoBytes,
        byte[]? headshotBytes,
        CancellationToken ct)
    {
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"cma-{lead.Id}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf");

        _logger.LogInformation(
            "[CMA-PDF-001] Generating CMA PDF for lead {LeadId}, report type {ReportType}, output {Path}",
            lead.Id, reportType, outputPath);

        try
        {
            await Task.Run(() => GenerateSync(lead, analysis, comps, config, reportType, logoBytes, headshotBytes, outputPath), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[CMA-PDF-003] Error generating CMA PDF for lead {LeadId}",
                lead.Id);
            throw;
        }

        _logger.LogInformation(
            "[CMA-PDF-002] CMA PDF complete for lead {LeadId}, path {Path}",
            lead.Id, outputPath);

        return outputPath;
    }

    private static void GenerateSync(
        Lead lead,
        CmaAnalysis analysis,
        List<Comp> comps,
        AccountConfig config,
        ReportType reportType,
        byte[]? logoBytes,
        byte[]? headshotBytes,
        string outputPath)
    {
        var fullAddress = BuildFullAddress(lead.SellerDetails);
        var primaryColor = HexOrDefault(config.Branding?.PrimaryColor, "#2E7D32");

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginTop(0);
                page.MarginBottom(0);
                page.MarginHorizontal(0);
                page.DefaultTextStyle(t => t.FontSize(9).FontColor(Colors.Black));

                // Header band — compact, full width
                page.Header().Element(c => AddHeaderBand(c, config, logoBytes, headshotBytes, primaryColor));

                page.Content().PaddingHorizontal(30).Column(col =>
                {
                    col.Item().PaddingTop(12);

                    // Section 2: Property Overview
                    AddPropertyOverview(col, lead.SellerDetails, fullAddress, primaryColor);

                    // Section 3: Value Estimate
                    AddValueEstimate(col, analysis, primaryColor);

                    // Section 4: Comparable Sales Table
                    AddCompTable(col, comps, primaryColor);

                    // Section 5: Market Analysis
                    AddMarketAnalysis(col, analysis, primaryColor);

                    // Section 6: Pricing Strategy + Lead Insights
                    if (reportType is ReportType.Comprehensive)
                        AddPricingStrategy(col, analysis);
                });

                // Section 7: Footer
                page.Footer().Element(c => AddFooter(c, config, logoBytes, primaryColor));
            });
        }).GeneratePdf(outputPath);
    }

    // -------------------------------------------------------------------------
    // Section 1: Header Band
    // -------------------------------------------------------------------------

    private static void AddHeaderBand(
        IContainer container,
        AccountConfig config,
        byte[]? logoBytes,
        byte[]? headshotBytes,
        string primaryColor)
    {
        container
            .Background(primaryColor)
            .PaddingHorizontal(16)
            .PaddingVertical(8)
            .Row(row =>
            {
                // Left: brokerage logo with white background for readability
                row.ConstantItem(120).AlignMiddle().Column(c =>
                {
                    if (logoBytes is { Length: > 0 })
                    {
                        c.Item().Background(Colors.White).Padding(4)
                            .Width(112).Height(50).Image(logoBytes).FitArea();
                    }
                    else if (config.Brokerage?.Name is { } brokerageName)
                    {
                        c.Item().Text(brokerageName)
                            .FontSize(9).Bold().FontColor(Colors.White);
                    }
                });

                // Center: CMA title + agent name + license (compact)
                row.RelativeItem().AlignCenter().AlignMiddle().Column(c =>
                {
                    c.Item().Text("Comparative Market Analysis")
                        .FontSize(9).FontColor(Colors.White).Italic();

                    c.Item().Text(config.Agent?.Name ?? "")
                        .FontSize(11).Bold().FontColor(Colors.White);

                    var subtitle = string.Join(" | ",
                        new[] { config.Agent?.Title, config.Agent?.LicenseNumber is { } lic ? $"Lic# {lic}" : null }
                        .Where(s => s is not null));
                    if (subtitle.Length > 0)
                        c.Item().Text(subtitle).FontSize(8).FontColor(Colors.White);
                });

                // Right: headshot (compact)
                row.ConstantItem(56).AlignRight().AlignMiddle().Column(c =>
                {
                    if (headshotBytes is { Length: > 0 })
                    {
                        c.Item().Width(50).Height(50).Image(headshotBytes).FitArea();
                    }
                });
            });
    }

    // -------------------------------------------------------------------------
    // Section 2: Property Overview
    // -------------------------------------------------------------------------

    private static void AddPropertyOverview(
        ColumnDescriptor col,
        SellerDetails? sd,
        string fullAddress,
        string primaryColor)
    {
        col.Item().PaddingBottom(6).Text("Property Overview")
            .FontSize(11).Bold().FontColor(primaryColor);

        col.Item().PaddingBottom(10).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Address").FontSize(8).FontColor(Colors.Grey.Medium).Bold();
                c.Item().Text(fullAddress).FontSize(11);
            });

            row.ConstantItem(1).Background(Colors.Grey.Lighten2);

            row.ConstantItem(16);

            row.ConstantItem(60).Column(c =>
            {
                c.Item().Text("Beds").FontSize(8).FontColor(Colors.Grey.Medium).Bold();
                c.Item().Text(sd?.Beds?.ToString() ?? "—").FontSize(11);
            });

            row.ConstantItem(60).Column(c =>
            {
                c.Item().Text("Baths").FontSize(8).FontColor(Colors.Grey.Medium).Bold();
                c.Item().Text(sd?.Baths?.ToString() ?? "—").FontSize(11);
            });

            row.ConstantItem(80).Column(c =>
            {
                c.Item().Text("Sq Ft").FontSize(8).FontColor(Colors.Grey.Medium).Bold();
                c.Item().Text(sd?.Sqft?.ToString("N0") ?? "—").FontSize(11);
            });

        });
    }

    // -------------------------------------------------------------------------
    // Section 3: Value Estimate
    // -------------------------------------------------------------------------

    private static void AddValueEstimate(
        ColumnDescriptor col,
        CmaAnalysis analysis,
        string primaryColor)
    {
        col.Item().PaddingBottom(6).Text("Estimated Value Range")
            .FontSize(11).Bold().FontColor(primaryColor);

        col.Item().PaddingBottom(8).Row(row =>
        {
            // Low
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
            {
                c.Item().Text("LOW").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                c.Item().PaddingTop(4).Text(FormatCurrency(analysis.ValueLow)).FontSize(16).Bold();
            });

            row.ConstantItem(8);

            // Mid — hero card
            row.RelativeItem().Background(primaryColor).Padding(12).Column(c =>
            {
                c.Item().Text("MID (RECOMMENDED)").FontSize(8).Bold().FontColor(Colors.White);
                c.Item().PaddingTop(4).Text(FormatCurrency(analysis.ValueMid)).FontSize(22).Bold().FontColor(Colors.White);
            });

            row.ConstantItem(8);

            // High
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(c =>
            {
                c.Item().Text("HIGH").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                c.Item().PaddingTop(4).Text(FormatCurrency(analysis.ValueHigh)).FontSize(16).Bold();
            });
        });

        // Market trend badge
        col.Item().PaddingBottom(8).Row(row =>
        {
            row.AutoItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
            {
                c.Item().Text($"Market Trend: {analysis.MarketTrend}").FontSize(9).Bold();
            });

            row.ConstantItem(12);

            row.AutoItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Column(c =>
            {
                c.Item().Text($"Median Days on Market: {analysis.MedianDaysOnMarket}").FontSize(9).Bold();
            });
        });
    }

    // -------------------------------------------------------------------------
    // Section 4: Comparable Sales Table
    // -------------------------------------------------------------------------

    private static void AddCompTable(ColumnDescriptor col, List<Comp> comps, string primaryColor)
    {
        col.Item().PaddingBottom(6).Text("Recent Comparable Sales")
            .FontSize(11).Bold().FontColor(primaryColor);

        var hasOlderComps = comps.Any(c => !c.IsRecent);

        col.Item().PaddingBottom(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);   // Address
                cols.RelativeColumn(2);   // Sale Price
                cols.RelativeColumn();    // Age (months)
                cols.RelativeColumn();    // Sale Date
                cols.RelativeColumn();    // Beds
                cols.RelativeColumn();    // Baths
                cols.RelativeColumn(2);   // SqFt
                cols.RelativeColumn(2);   // $/SqFt
                cols.RelativeColumn(2);   // Distance
            });

            table.Header(header =>
            {
                void HeaderCell(string text) =>
                    header.Cell()
                        .Background(primaryColor)
                        .Padding(4)
                        .Text(text)
                        .FontSize(8).Bold().FontColor(Colors.White);

                HeaderCell("Address");
                HeaderCell("Sale Price");
                HeaderCell("Age");
                HeaderCell("Sale Date");
                HeaderCell("Bd");
                HeaderCell("Ba");
                HeaderCell("Sq Ft");
                HeaderCell("$/Sq Ft");
                HeaderCell("Distance");
            });

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            foreach (var (comp, index) in comps.Select((c, i) => (c, i)))
            {
                var rowBg = index % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                var ageMonths = MonthsBetween(comp.SaleDate, today);
                var ageSuffix = comp.IsRecent ? "" : " †";

                void DataCell(string text, bool italic = false)
                {
                    var cell = table.Cell().Background(rowBg).Padding(4);
                    var t = cell.Text(text).FontSize(9);
                    if (italic) t.Italic();
                }

                DataCell(comp.Address);
                DataCell(FormatCurrency(comp.SalePrice));
                DataCell($"{ageMonths}mo{ageSuffix}");
                DataCell(comp.SaleDate.ToString("MM/dd/yyyy"));
                DataCell(comp.Beds.ToString());
                DataCell(comp.Baths.ToString());
                DataCell(comp.Sqft.ToString("N0"));
                DataCell(FormatPricePerSqft(comp.PricePerSqft));
                DataCell($"{comp.DistanceMiles:F1} mi");
            }
        });

        if (hasOlderComps)
        {
            col.Item().PaddingTop(4).PaddingBottom(12).Text("† Older sale — weighted less in analysis")
                .FontSize(8).Italic().FontColor(Colors.Grey.Medium);
        }
        else
        {
            col.Item().PaddingBottom(12);
        }
    }

    // -------------------------------------------------------------------------
    // Section 5: Market Analysis
    // -------------------------------------------------------------------------

    private static void AddMarketAnalysis(ColumnDescriptor col, CmaAnalysis analysis, string primaryColor)
    {
        col.Item().PaddingBottom(6).Text("Market Analysis")
            .FontSize(11).Bold().FontColor(primaryColor);

        col.Item().PaddingBottom(8).Text(analysis.MarketNarrative).FontSize(10);
    }

    // -------------------------------------------------------------------------
    // Section 6: Pricing Strategy
    // -------------------------------------------------------------------------

    private static void AddPricingStrategy(ColumnDescriptor col, CmaAnalysis analysis)
    {
        if (analysis.PricingStrategy is null && analysis.PricingRecommendation is null && analysis.LeadInsights is null)
            return;

        // Render PricingStrategy (always present per system prompt rule 9)
        if (analysis.PricingStrategy is { } strategy)
        {
            col.Item().ShowEntire().Column(section =>
            {
                section.Item().PaddingBottom(6).Text("Pricing Strategy").FontSize(11).Bold();
                section.Item().PaddingBottom(12).Text(strategy).FontSize(10);
            });
        }

        // Fallback to PricingRecommendation if PricingStrategy is null (legacy)
        if (analysis.PricingStrategy is null && analysis.PricingRecommendation is { } recommendation)
        {
            col.Item().ShowEntire().Column(section =>
            {
                section.Item().PaddingBottom(6).Text("Pricing Strategy").FontSize(11).Bold();
                section.Item().PaddingBottom(12).Text(recommendation).FontSize(10);
            });
        }

        if (analysis.LeadInsights is { } insights)
        {
            col.Item().ShowEntire().Column(section =>
            {
                section.Item().PaddingBottom(6).Text("Seller Insights").FontSize(11).Bold();
                section.Item().PaddingBottom(12).Text(insights).FontSize(10);
            });
        }
    }

    // -------------------------------------------------------------------------
    // Section 7: Footer
    // -------------------------------------------------------------------------

    private static void AddFooter(IContainer container, AccountConfig config, byte[]? logoBytes, string primaryColor)
    {
        container
            .PaddingHorizontal(30)
            .PaddingVertical(6)
            .Column(col =>
            {
                col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
                col.Item().PaddingTop(4).Row(row =>
                {
                    // Left: agent contact
                    var contactParts = new List<string>();
                    if (config.Agent?.Name is { } name) contactParts.Add(name);
                    if (config.Brokerage?.Name is { } brokerage) contactParts.Add(brokerage);
                    if (config.Agent?.Phone is { } phone && phone.Length > 0) contactParts.Add(phone);
                    if (config.Agent?.LicenseNumber is { } lic) contactParts.Add($"Lic# {lic}");

                    row.RelativeItem().Text(string.Join(" | ", contactParts))
                        .FontSize(7).FontColor(Colors.Grey.Darken1);

                    // Right: disclaimer + date
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.Span("This is not an appraisal. ").FontSize(6).Italic().FontColor(Colors.Grey.Medium);
                        t.Span($"Generated {DateTime.UtcNow:MMM d, yyyy}").FontSize(6).FontColor(Colors.Grey.Medium);
                    });
                });
            });
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    internal static string BuildFullAddress(SellerDetails? sd)
    {
        if (sd is null) return "Address not provided";
        return $"{sd.Address}, {sd.City}, {sd.State} {sd.Zip}";
    }

    internal static string FormatCurrency(decimal value) =>
        value.ToString("C0", EnUs);

    internal static string FormatPricePerSqft(decimal value) =>
        value.ToString("C2", EnUs);

    /// <summary>
    /// Parses a CSS hex color (e.g. "#2E7D32") for use as a QuestPDF color string.
    /// Falls back to <paramref name="fallback"/> if <paramref name="hex"/> is null or malformed.
    /// </summary>
    internal static string HexOrDefault(string? hex, string fallback)
    {
        if (hex is null) return fallback;
        var trimmed = hex.TrimStart('#');
        return trimmed.Length is 6 or 8 ? $"#{trimmed}" : fallback;
    }

    /// <summary>Returns the number of whole months between two dates.</summary>
    private static int MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = (to.Year - from.Year) * 12 + (to.Month - from.Month);
        return Math.Max(0, months);
    }
}
