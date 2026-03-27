using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Workers.Leads;

/// <summary>
/// Background service that generates CMA PDF documents from <see cref="CmaWorkerResult"/> JSON
/// and persists them via <see cref="IDocumentStorageProvider"/>. Resolves the per-request
/// <see cref="TaskCompletionSource{T}"/> so callers can await the result directly.
/// </summary>
public class PdfWorker(
    PdfProcessingChannel channel,
    IDocumentStorageProvider documentStorage,
    ILogger<PdfWorker> logger)
    : BackgroundService
{
    private static readonly CultureInfo EnUs = new("en-US");

    static PdfWorker()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[PdfWorker-001] PdfWorker started.");

        await foreach (var request in channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessRequestAsync(request, stoppingToken);
        }

        logger.LogInformation("[PdfWorker-003] PdfWorker stopping.");
    }

    internal async Task ProcessRequestAsync(PdfProcessingRequest request, CancellationToken ct)
    {
        try
        {
            var pdfBytes = GeneratePdfBytes(request.CmaResult, request.AgentConfig);
            var storagePath = await StorePdfAsync(request.LeadId, pdfBytes, ct);

            logger.LogInformation(
                "[PdfWorker-010] CMA PDF generated and stored. LeadId: {LeadId}, Path: {Path}, CorrelationId: {CorrelationId}",
                request.LeadId, storagePath, request.CorrelationId);

            request.Completion.TrySetResult(new PdfWorkerResult(
                request.LeadId, Success: true, Error: null, StoragePath: storagePath));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[PdfWorker-020] Failed to generate or store CMA PDF. LeadId: {LeadId}, CorrelationId: {CorrelationId}",
                request.LeadId, request.CorrelationId);

            request.Completion.TrySetResult(new PdfWorkerResult(
                request.LeadId, Success: false, Error: ex.Message, StoragePath: null));
        }
    }

    internal async Task<string> StorePdfAsync(string leadId, byte[] pdfBytes, CancellationToken ct)
    {
        var timestamp = DateTime.UtcNow;
        var folder = $"Real Estate Star/1 - Leads/{leadId}/CMA";
        var fileName = $"{timestamp:yyyy-MM-dd}-{leadId}-CMA-Report.pdf.b64";

        await documentStorage.WriteDocumentAsync(
            folder,
            fileName,
            Convert.ToBase64String(pdfBytes),
            ct);

        return $"{folder}/{fileName}";
    }

    internal static byte[] GeneratePdfBytes(CmaWorkerResult cmaResult, AgentNotificationConfig agentConfig)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(40);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Black));

                page.Content().Column(col =>
                {
                    AddCoverSection(col, cmaResult, agentConfig);
                    AddValueRangeSection(col, cmaResult);
                    AddCompsSection(col, cmaResult);
                    AddMarketAnalysisSection(col, cmaResult);
                    AddAgentFooter(col, agentConfig);
                });
            });
        }).GeneratePdf();
    }

    private static void AddCoverSection(
        ColumnDescriptor col,
        CmaWorkerResult cmaResult,
        AgentNotificationConfig agentConfig)
    {
        col.Item().PaddingBottom(20).Column(inner =>
        {
            inner.Item().Text("Comparative Market Analysis")
                .FontSize(26).Bold().FontColor(Colors.Grey.Darken3);

            inner.Item().PaddingTop(8).Text(cmaResult.LeadId)
                .FontSize(14).FontColor(Colors.Grey.Darken2);

            inner.Item().PaddingTop(20).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("Prepared by:").Bold();
                    c.Item().Text(agentConfig.Name);
                    c.Item().Text(agentConfig.BrokerageName).FontColor(Colors.Grey.Medium);
                    c.Item().Text(agentConfig.Phone);
                    c.Item().Text(agentConfig.Email);
                });
            });
        });

        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        col.Item().PaddingBottom(16);
    }

    private static void AddValueRangeSection(ColumnDescriptor col, CmaWorkerResult cmaResult)
    {
        col.Item().PaddingBottom(8).Text("Estimated Value Range")
            .FontSize(14).Bold();

        col.Item().PaddingBottom(4).Row(row =>
        {
            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
            {
                c.Item().Text("Low").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                c.Item().Text(FormatCurrency(cmaResult.PriceRangeLow ?? 0))
                    .FontSize(14).Bold();
            });

            row.ConstantItem(8);

            row.RelativeItem().Border(1).BorderColor(Colors.Blue.Medium).Padding(8).Column(c =>
            {
                c.Item().Text("Mid").Bold().FontSize(9).FontColor(Colors.Blue.Medium);
                c.Item().Text(FormatCurrency(cmaResult.EstimatedValue ?? 0))
                    .FontSize(16).Bold().FontColor(Colors.Blue.Darken2);
            });

            row.ConstantItem(8);

            row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
            {
                c.Item().Text("High").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                c.Item().Text(FormatCurrency(cmaResult.PriceRangeHigh ?? 0))
                    .FontSize(14).Bold();
            });
        });

        col.Item().PaddingBottom(16);
    }

    private static void AddCompsSection(ColumnDescriptor col, CmaWorkerResult cmaResult)
    {
        if (cmaResult.Comps is not { Count: > 0 } comps) return;

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
            });

            table.Header(header =>
            {
                header.Cell().Text("Address").Bold();
                header.Cell().Text("Sale Price").Bold();
                header.Cell().Text("Bd").Bold();
                header.Cell().Text("Ba").Bold();
                header.Cell().Text("Sqft").Bold();
            });

            foreach (var comp in comps)
            {
                table.Cell().Text(comp.Address);
                table.Cell().Text(FormatCurrency(comp.Price));
                table.Cell().Text(comp.Beds?.ToString() ?? "\u2014");
                table.Cell().Text(comp.Baths?.ToString() ?? "\u2014");
                table.Cell().Text(comp.Sqft?.ToString("N0") ?? "\u2014");
            }
        });

        col.Item().PaddingBottom(16);
    }

    private static void AddMarketAnalysisSection(ColumnDescriptor col, CmaWorkerResult cmaResult)
    {
        if (string.IsNullOrWhiteSpace(cmaResult.MarketAnalysis)) return;

        col.Item().PaddingBottom(8).Text("Market Analysis")
            .FontSize(14).Bold();

        col.Item().PaddingBottom(16).Text(cmaResult.MarketAnalysis);
    }

    private static void AddAgentFooter(ColumnDescriptor col, AgentNotificationConfig agentConfig)
    {
        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        col.Item().PaddingTop(16).Column(c =>
        {
            c.Item().Text(agentConfig.Name).Bold().FontSize(12);
            c.Item().Text(agentConfig.BrokerageName).FontColor(Colors.Grey.Medium);
            c.Item().Text($"License: {agentConfig.LicenseNumber}").FontColor(Colors.Grey.Medium);
            c.Item().Text(agentConfig.Phone);
            c.Item().Text(agentConfig.Email);

            if (agentConfig.ServiceAreas.Count > 0)
            {
                c.Item().PaddingTop(4).Text(
                    $"Serving: {string.Join(", ", agentConfig.ServiceAreas)}").FontColor(Colors.Grey.Darken1);
            }
        });
    }

    internal static string FormatCurrency(decimal value) =>
        value.ToString("C0", EnUs);
}
