using System.Globalization;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RealEstateStar.Api.Common;

namespace RealEstateStar.Api.Features.Leads.Cma;

public class CmaPdfGenerator : ICmaPdfGenerator
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

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
        AccountConfig agent,
        ReportType reportType,
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
            await Task.Run(() => GenerateSync(lead, analysis, comps, agent, reportType, outputPath), ct);
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
        AccountConfig agent,
        ReportType reportType,
        string outputPath)
    {
        var fullAddress = BuildFullAddress(lead.SellerDetails);
        var sd = lead.SellerDetails;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Black));

                page.Content().Column(col =>
                {
                    // Cover Page
                    AddCoverPage(col, lead, fullAddress, agent);

                    // Property Overview (Standard / Comprehensive)
                    if (reportType is ReportType.Standard or ReportType.Comprehensive)
                        AddPropertyOverview(col, sd, fullAddress);

                    // Comp Table — always included
                    AddCompTable(col, comps);

                    // Market Analysis (Comprehensive)
                    if (reportType is ReportType.Comprehensive)
                        AddMarketAnalysis(col, analysis);

                    // Price Per Sqft Analysis (Comprehensive)
                    if (reportType is ReportType.Comprehensive)
                        AddPricePerSqftAnalysis(col, comps, sd);

                    // Value Estimate — always included
                    AddValueEstimate(col, analysis, reportType);

                    // About Agent — always included
                    AddAboutAgent(col, agent);
                });
            });
        }).GeneratePdf(outputPath);
    }

    internal static string BuildFullAddress(SellerDetails? sd)
    {
        if (sd is null) return "Address not provided";
        return $"{sd.Address}, {sd.City}, {sd.State} {sd.Zip}";
    }

    internal static string FormatCurrency(decimal value) =>
        value.ToString("C0", EnUs);

    internal static string FormatPricePerSqft(decimal value) =>
        value.ToString("C2", EnUs);

    private static void AddCoverPage(
        ColumnDescriptor col,
        Lead lead,
        string fullAddress,
        AccountConfig agent)
    {
        col.Item().PaddingBottom(20).Column(inner =>
        {
            inner.Item().Text("Comparative Market Analysis")
                .FontSize(26).Bold().FontColor(Colors.Grey.Darken3);

            inner.Item().PaddingTop(8).Text(fullAddress)
                .FontSize(14).FontColor(Colors.Grey.Darken2);

            inner.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Prepared for:").Bold();
                    c.Item().Text(lead.FullName);
                });

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Prepared by:").Bold();
                    c.Item().Text(agent.Agent?.Name ?? "");
                    if (agent.Agent?.Title is { } title)
                        c.Item().Text(title).FontColor(Colors.Grey.Medium);
                    if (agent.Brokerage?.Name is { } brokerage)
                        c.Item().Text(brokerage).FontColor(Colors.Grey.Medium);
                    c.Item().Text(agent.Agent?.Phone ?? "");
                    c.Item().Text(agent.Agent?.Email ?? "");
                });
            });
        });

        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        col.Item().PaddingBottom(16);
    }

    private static void AddPropertyOverview(
        ColumnDescriptor col,
        SellerDetails? sd,
        string fullAddress)
    {
        col.Item().PaddingBottom(8).Text("Property Overview")
            .FontSize(14).Bold();

        col.Item().PaddingBottom(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2);
                cols.RelativeColumn();
                cols.RelativeColumn();
                cols.RelativeColumn();
            });

            table.Header(header =>
            {
                header.Cell().Text("Address").Bold();
                header.Cell().Text("Beds").Bold();
                header.Cell().Text("Baths").Bold();
                header.Cell().Text("Sqft").Bold();
            });

            table.Cell().Text(fullAddress);
            table.Cell().Text(sd?.Beds?.ToString() ?? "—");
            table.Cell().Text(sd?.Baths?.ToString() ?? "—");
            table.Cell().Text(sd?.Sqft?.ToString("N0") ?? "—");
        });

        col.Item().PaddingBottom(16);
    }

    private static void AddCompTable(ColumnDescriptor col, List<Comp> comps)
    {
        col.Item().PaddingBottom(8).Text("Comparable Sales")
            .FontSize(14).Bold();

        col.Item().PaddingBottom(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);
                cols.RelativeColumn(2);
                cols.RelativeColumn();
                cols.RelativeColumn();
                cols.RelativeColumn();
                cols.RelativeColumn(2);
                cols.RelativeColumn();
                cols.RelativeColumn();
            });

            table.Header(header =>
            {
                header.Cell().Text("Address").Bold();
                header.Cell().Text("Sale Price").Bold();
                header.Cell().Text("Bd").Bold();
                header.Cell().Text("Ba").Bold();
                header.Cell().Text("Sqft").Bold();
                header.Cell().Text("$/Sqft").Bold();
                header.Cell().Text("Date").Bold();
                header.Cell().Text("Source").Bold();
            });

            foreach (var comp in comps)
            {
                table.Cell().Text(comp.Address);
                table.Cell().Text(FormatCurrency(comp.SalePrice));
                table.Cell().Text(comp.Beds.ToString());
                table.Cell().Text(comp.Baths.ToString());
                table.Cell().Text(comp.Sqft.ToString("N0"));
                table.Cell().Text(FormatPricePerSqft(comp.PricePerSqft));
                table.Cell().Text(comp.SaleDate.ToString("MM/dd/yyyy"));
                table.Cell().Text(comp.Source.ToString());
            }
        });

        col.Item().PaddingBottom(16);
    }

    private static void AddMarketAnalysis(ColumnDescriptor col, CmaAnalysis analysis)
    {
        col.Item().PaddingBottom(8).Text("Market Analysis")
            .FontSize(14).Bold();

        col.Item().PaddingBottom(4).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Market Trend:").Bold();
                c.Item().Text(analysis.MarketTrend);
            });

            row.ConstantItem(20);

            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Median Days on Market:").Bold();
                c.Item().Text(analysis.MedianDaysOnMarket.ToString());
            });
        });

        col.Item().PaddingTop(8).Text("Market Narrative:").Bold();
        col.Item().PaddingBottom(4).Text(analysis.MarketNarrative);

        col.Item().PaddingBottom(16);
    }

    private static void AddPricePerSqftAnalysis(
        ColumnDescriptor col,
        List<Comp> comps,
        SellerDetails? sd)
    {
        col.Item().PaddingBottom(8).Text("Price Per Sqft Analysis")
            .FontSize(14).Bold();

        col.Item().PaddingBottom(4).Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(3);
                cols.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Text("Address").Bold();
                header.Cell().Text("$/Sqft").Bold();
            });

            foreach (var comp in comps)
            {
                table.Cell().Text(comp.Address);
                table.Cell().Text(FormatPricePerSqft(comp.PricePerSqft));
            }
        });

        if (comps.Count > 0)
        {
            var avgPricePerSqft = comps.Average(c => (double)c.PricePerSqft);
            col.Item().PaddingTop(4).Text($"Average $/Sqft: {FormatPricePerSqft((decimal)avgPricePerSqft)}").Bold();

            if (sd?.Sqft is { } sqft and > 0)
            {
                var estimatedValue = (decimal)avgPricePerSqft * sqft;
                col.Item().Text($"Applied to subject ({sqft:N0} sqft): {FormatCurrency(estimatedValue)}");
            }
        }

        col.Item().PaddingBottom(16);
    }

    private static void AddValueEstimate(
        ColumnDescriptor col,
        CmaAnalysis analysis,
        ReportType reportType)
    {
        col.Item().PaddingBottom(8).Text("Estimated Value Range")
            .FontSize(14).Bold();

        col.Item().PaddingBottom(4).Row(row =>
        {
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
            {
                c.Item().Text("Low").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                c.Item().Text(FormatCurrency(analysis.ValueLow)).FontSize(14).Bold();
            });

            row.ConstantItem(8);

            row.RelativeItem().Border(1).BorderColor(Colors.Blue.Medium).Padding(8).Column(c =>
            {
                c.Item().Text("Mid").Bold().FontSize(9).FontColor(Colors.Blue.Medium);
                c.Item().Text(FormatCurrency(analysis.ValueMid)).FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
            });

            row.ConstantItem(8);

            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
            {
                c.Item().Text("High").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                c.Item().Text(FormatCurrency(analysis.ValueHigh)).FontSize(14).Bold();
            });
        });

        if (reportType is ReportType.Comprehensive && analysis.PricingRecommendation is { } recommendation)
        {
            col.Item().PaddingTop(8).Text("Pricing Strategy:").Bold();
            col.Item().PaddingBottom(4).Text(recommendation);
        }

        col.Item().PaddingBottom(16);
    }

    private static void AddAboutAgent(ColumnDescriptor col, AccountConfig agent)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        col.Item().PaddingTop(16).PaddingBottom(8).Text("About Your Agent")
            .FontSize(14).Bold();

        col.Item().Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text(agent.Agent?.Name ?? "").Bold().FontSize(12);

                if (agent.Agent?.Title is { } title)
                    c.Item().Text(title).FontColor(Colors.Grey.Medium);

                if (agent.Brokerage?.Name is { } brokerage)
                    c.Item().Text(brokerage).FontColor(Colors.Grey.Medium);

                if (agent.Agent?.Tagline is { } tagline)
                    c.Item().PaddingTop(4).Text(tagline).Italic().FontColor(Colors.Grey.Darken1);

                c.Item().PaddingTop(8).Text(agent.Agent?.Phone ?? "");
                c.Item().Text(agent.Agent?.Email ?? "");
            });

            row.ConstantItem(20);

            row.RelativeItem().Column(c =>
            {
                var serviceAreas = agent.Location?.ServiceAreas ?? [];
                if (serviceAreas.Count > 0)
                {
                    c.Item().Text("Service Areas:").Bold();
                    c.Item().Text(string.Join(", ", serviceAreas));
                }

                var languages = agent.Agent?.Languages ?? [];
                if (languages.Count > 0)
                {
                    c.Item().PaddingTop(8).Text("Languages:").Bold();
                    c.Item().Text(string.Join(", ", languages));
                }
            });
        });
    }
}
