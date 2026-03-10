using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RealEstateStar.Api.Models;

namespace RealEstateStar.Api.Services.Pdf;

public class CmaPdfGenerator : ICmaPdfGenerator
{
    static CmaPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public void Generate(PdfGenerationRequest request, CancellationToken ct)
    {
        var document = Document.Create(container =>
        {
            ComposeCoverPage(container, request.Agent, request.Lead);

            if (request.ReportType is ReportType.Standard or ReportType.Comprehensive)
                ComposePropertyOverviewPage(container, request.Lead, request.Research);

            ComposeCompTablePage(container, request.Comps);

            if (request.ReportType is ReportType.Comprehensive)
            {
                ComposeMarketAnalysisPage(container, request.Analysis);
                ComposePricePerSqftPage(container, request.Lead, request.Comps);
            }

            ComposeValueEstimatePage(container, request.Analysis, request.ReportType);

            if (request.ReportType is ReportType.Comprehensive)
                ComposeNeighborhoodPage(container, request.Research);

            ComposeAboutAgentPage(container, request.Agent);
        });

        document.GeneratePdf(request.OutputPath);
    }

    private static void ComposeCoverPage(IDocumentContainer container, AgentConfig agent, Lead lead)
    {
        container.Page(page =>
        {
            ConfigurePage(page);

            page.Content().Column(col =>
            {
                col.Item().PaddingTop(120).AlignCenter().Text("Comparative Market Analysis")
                    .FontSize(28).Bold().FontColor(Colors.Grey.Darken3);

                col.Item().PaddingTop(30).AlignCenter().Text(lead.FullAddress)
                    .FontSize(16).FontColor(Colors.Grey.Darken2);

                col.Item().PaddingTop(60).AlignCenter().Text($"Prepared for {lead.FullName}")
                    .FontSize(14).FontColor(Colors.Grey.Darken1);

                col.Item().PaddingTop(10).AlignCenter().Text($"Prepared by {agent.Identity?.Name ?? "Agent"}")
                    .FontSize(14).FontColor(Colors.Grey.Darken1);

                if (agent.Identity?.Brokerage is { } brokerage)
                    col.Item().PaddingTop(6).AlignCenter().Text(brokerage)
                        .FontSize(12).FontColor(Colors.Grey.Medium);

                col.Item().PaddingTop(30).AlignCenter().Text(DateTime.Now.ToString("MMMM d, yyyy"))
                    .FontSize(12).FontColor(Colors.Grey.Medium);

                col.Item().PaddingTop(40).AlignCenter().Column(contact =>
                {
                    if (agent.Identity?.Phone is { } phone)
                        contact.Item().AlignCenter().Text(phone).FontSize(11).FontColor(Colors.Grey.Darken1);
                    if (agent.Identity?.Email is { } email)
                        contact.Item().AlignCenter().Text(email).FontSize(11).FontColor(Colors.Grey.Darken1);
                    if (agent.Identity?.Website is { } website)
                        contact.Item().AlignCenter().Text(website).FontSize(11).FontColor(Colors.Grey.Darken1);
                });
            });

            ComposeFooter(page);
        });
    }

    private static void ComposePropertyOverviewPage(IDocumentContainer container, Lead lead, LeadResearch? research)
    {
        container.Page(page =>
        {
            ConfigurePage(page);
            page.Header().PaddingBottom(10).Text("Property Overview")
                .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

            page.Content().Column(col =>
            {
                col.Item().PaddingBottom(10).Text(lead.FullAddress)
                    .FontSize(14).SemiBold();

                ComposeDetailRow(col, "Bedrooms", lead.Beds?.ToString() ?? "N/A");
                ComposeDetailRow(col, "Bathrooms", lead.Baths?.ToString() ?? "N/A");
                ComposeDetailRow(col, "Square Feet", lead.Sqft?.ToString("N0") ?? "N/A");
                ComposeDetailRow(col, "Year Built", research?.YearBuilt?.ToString() ?? "N/A");

                if (research?.LotSize is { } lotSize)
                    ComposeDetailRow(col, "Lot Size", $"{lotSize:N2} {research.LotSizeUnit ?? "acres"}");

                if (research?.TaxAssessment is { } taxAssessment)
                    ComposeDetailRow(col, "Tax Assessment", taxAssessment.ToString("C0"));
            });

            ComposeFooter(page);
        });
    }

    private static void ComposeCompTablePage(IDocumentContainer container, List<Comp> comps)
    {
        container.Page(page =>
        {
            ConfigurePage(page);
            page.Header().PaddingBottom(10).Text("Comparable Sales")
                .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);  // Address
                    cols.RelativeColumn(2);  // Sale Price
                    cols.RelativeColumn(1);  // Bd/Ba
                    cols.RelativeColumn(1);  // Sqft
                    cols.RelativeColumn(1);  // $/Sqft
                    cols.RelativeColumn(2);  // Sale Date
                    cols.RelativeColumn(1);  // Source
                });

                table.Header(header =>
                {
                    foreach (var h in new[] { "Address", "Sale Price", "Bd/Ba", "Sqft", "$/Sqft", "Sale Date", "Source" })
                    {
                        header.Cell().PaddingVertical(5).PaddingHorizontal(3)
                            .Background(Colors.Grey.Lighten3)
                            .Text(h).FontSize(9).SemiBold();
                    }
                });

                foreach (var comp in comps)
                {
                    ComposeTableCell(table, comp.Address);
                    ComposeTableCell(table, comp.SalePrice.ToString("C0"));
                    ComposeTableCell(table, $"{comp.Beds}/{comp.Baths}");
                    ComposeTableCell(table, comp.Sqft.ToString("N0"));
                    ComposeTableCell(table, comp.PricePerSqft.ToString("C0"));
                    ComposeTableCell(table, comp.SaleDate.ToString("MM/dd/yyyy"));
                    ComposeTableCell(table, comp.Source.ToString());
                }
            });

            ComposeFooter(page);
        });
    }

    private static void ComposeMarketAnalysisPage(IDocumentContainer container, CmaAnalysis analysis)
    {
        container.Page(page =>
        {
            ConfigurePage(page);
            page.Header().PaddingBottom(10).Text("Market Analysis")
                .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

            page.Content().Column(col =>
            {
                col.Item().PaddingBottom(15).Text(analysis.MarketNarrative)
                    .FontSize(11).LineHeight(1.5f);

                ComposeDetailRow(col, "Market Trend", analysis.MarketTrend);
                ComposeDetailRow(col, "Median Days on Market", analysis.MedianDaysOnMarket.ToString());
            });

            ComposeFooter(page);
        });
    }

    private static void ComposePricePerSqftPage(IDocumentContainer container, Lead lead, List<Comp> comps)
    {
        container.Page(page =>
        {
            ConfigurePage(page);
            page.Header().PaddingBottom(10).Text("Price Per Square Foot Analysis")
                .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

            page.Content().Column(col =>
            {
                foreach (var comp in comps)
                {
                    col.Item().PaddingBottom(6).Text($"{comp.Address}:  {comp.PricePerSqft:C0}/sqft")
                        .FontSize(11);
                }

                var avgPricePerSqft = comps.Count > 0
                    ? comps.Average(c => c.PricePerSqft)
                    : 0m;

                col.Item().PaddingTop(15).Text($"Average $/Sqft: {avgPricePerSqft:C0}")
                    .FontSize(13).SemiBold();

                if (lead.Sqft is > 0)
                {
                    var estimatedValue = avgPricePerSqft * lead.Sqft.Value;
                    col.Item().PaddingTop(8).Text(
                        $"Applied to Subject ({lead.Sqft.Value:N0} sqft): {estimatedValue:C0}")
                        .FontSize(13).SemiBold();
                }
            });

            ComposeFooter(page);
        });
    }

    private static void ComposeValueEstimatePage(IDocumentContainer container, CmaAnalysis analysis, ReportType reportType)
    {
        container.Page(page =>
        {
            ConfigurePage(page);
            page.Header().PaddingBottom(10).Text("Estimated Value Range")
                .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

            page.Content().Column(col =>
            {
                col.Item().PaddingBottom(20).Row(row =>
                {
                    ComposeValueBox(row, "Low", analysis.ValueLow);
                    row.AutoItem().Width(20);
                    ComposeValueBox(row, "Mid", analysis.ValueMid);
                    row.AutoItem().Width(20);
                    ComposeValueBox(row, "High", analysis.ValueHigh);
                });

                if (reportType is ReportType.Comprehensive && analysis.PricingRecommendation is { } pricing)
                {
                    col.Item().PaddingTop(20).Text("Pricing Strategy")
                        .FontSize(16).SemiBold().FontColor(Colors.Grey.Darken3);

                    col.Item().PaddingTop(10).Text(pricing)
                        .FontSize(11).LineHeight(1.5f);
                }
            });

            ComposeFooter(page);
        });
    }

    private static void ComposeNeighborhoodPage(IDocumentContainer container, LeadResearch? research)
    {
        container.Page(page =>
        {
            ConfigurePage(page);
            page.Header().PaddingBottom(10).Text("Neighborhood Overview")
                .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

            var text = research?.NeighborhoodContext
                ?? "Neighborhood information is not available at this time. "
                + "Please contact your agent for more details about the local area.";

            page.Content().Text(text).FontSize(11).LineHeight(1.5f);

            ComposeFooter(page);
        });
    }

    private static void ComposeAboutAgentPage(IDocumentContainer container, AgentConfig agent)
    {
        container.Page(page =>
        {
            ConfigurePage(page);
            page.Header().PaddingBottom(10).Text("About Your Agent")
                .FontSize(20).Bold().FontColor(Colors.Grey.Darken3);

            page.Content().Column(col =>
            {
                col.Item().PaddingBottom(6).Text(agent.Identity?.Name ?? "Agent")
                    .FontSize(16).SemiBold();

                if (agent.Identity?.Title is { } title)
                    col.Item().PaddingBottom(4).Text(title).FontSize(12);

                if (agent.Identity?.Brokerage is { } brokerage)
                    col.Item().PaddingBottom(10).Text(brokerage)
                        .FontSize(12).FontColor(Colors.Grey.Darken1);

                if (agent.Location?.ServiceAreas is { Count: > 0 } areas)
                    col.Item().PaddingBottom(6)
                        .Text($"Service Areas: {string.Join(", ", areas)}")
                        .FontSize(11);

                if (agent.Identity?.Languages is { Count: > 0 } languages)
                    col.Item().PaddingBottom(6)
                        .Text($"Languages: {string.Join(", ", languages)}")
                        .FontSize(11);

                if (agent.Identity?.Tagline is { } tagline)
                    col.Item().PaddingTop(15).Text($"\"{tagline}\"")
                        .FontSize(13).Italic().FontColor(Colors.Grey.Darken2);

                col.Item().PaddingTop(30).Text("Ready to take the next step? Contact me today!")
                    .FontSize(12).SemiBold();

                col.Item().PaddingTop(8).Column(contact =>
                {
                    if (agent.Identity?.Phone is { } phone)
                        contact.Item().Text($"Phone: {phone}").FontSize(11);
                    if (agent.Identity?.Email is { } email)
                        contact.Item().Text($"Email: {email}").FontSize(11);
                    if (agent.Identity?.Website is { } website)
                        contact.Item().Text($"Web: {website}").FontSize(11);
                });
            });

            ComposeFooter(page);
        });
    }

    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.Letter);
        page.Margin(40);
    }

    private static void ComposeFooter(PageDescriptor page) =>
        page.Footer().AlignCenter().Text(text =>
        {
            text.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
            text.Span(" / ").FontSize(9).FontColor(Colors.Grey.Medium);
            text.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
        });

    private static void ComposeDetailRow(ColumnDescriptor col, string label, string value) =>
        col.Item().PaddingBottom(6).Row(row =>
        {
            row.RelativeItem(1).Text(label).FontSize(11).SemiBold();
            row.RelativeItem(2).Text(value).FontSize(11);
        });

    private static void ComposeTableCell(TableDescriptor table, string text) =>
        table.Cell().PaddingVertical(4).PaddingHorizontal(3)
            .BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
            .Text(text).FontSize(9);

    private static void ComposeValueBox(RowDescriptor row, string label, decimal value) =>
        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1)
            .Padding(15).Column(col =>
            {
                col.Item().AlignCenter().Text(label).FontSize(11).FontColor(Colors.Grey.Darken1);
                col.Item().AlignCenter().PaddingTop(5).Text(value.ToString("C0"))
                    .FontSize(18).Bold();
            });
}
