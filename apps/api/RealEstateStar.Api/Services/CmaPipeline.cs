using System.Globalization;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Models;
using RealEstateStar.Api.Services.Analysis;
using RealEstateStar.Api.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Services.Pdf;
using RealEstateStar.Api.Services.Research;

namespace RealEstateStar.Api.Services;

public class CmaPipeline(
    IAgentConfigService agentConfig,
    CompAggregator compAggregator,
    ILeadResearchService research,
    IAnalysisService analysis,
    ICmaPdfGenerator pdf,
    IGwsService gws,
    ILogger<CmaPipeline>? logger = null)
{
    public async Task<CmaJob?> ExecuteAsync(string agentId, Lead lead, Action<CmaJobStatus> onStatusChange)
    {
        // Step 1: Load agent config
        var agent = await agentConfig.GetAgentAsync(agentId);
        if (agent is null)
        {
            logger?.LogWarning("Agent {AgentId} not found — aborting pipeline", agentId);
            return null;
        }

        var agentGuid = Guid.TryParse(agent.Id, out var parsed) ? parsed : ToGuid(agent.Id);
        var job = CmaJob.Create(agentGuid, lead);
        Advance(job, CmaJobStatus.SearchingComps, onStatusChange);

        // Steps 2+3 in parallel: fetch comps + research lead
        var compsTask = compAggregator.FetchCompsAsync(
            lead.Address, lead.City, lead.State, lead.Zip,
            lead.Beds, lead.Baths, lead.Sqft);
        var researchTask = research.ResearchAsync(lead);

        await Task.WhenAll(compsTask, researchTask);

        var comps = await compsTask;
        var leadResearch = await researchTask;

        foreach (var comp in comps)
            job.Comps.Add(comp);
        job.LeadResearch = leadResearch;

        // Step 4: Claude analysis
        Advance(job, CmaJobStatus.Analyzing, onStatusChange);
        var cmaAnalysis = await analysis.AnalyzeAsync(lead, comps, leadResearch, job.ReportType);
        job.Analysis = cmaAnalysis;

        // Step 5: Generate PDF
        Advance(job, CmaJobStatus.GeneratingPdf, onStatusChange);
        var tempDir = Path.Combine(Path.GetTempPath(), "cma", job.Id.ToString());
        Directory.CreateDirectory(tempDir);
        var pdfPath = Path.Combine(tempDir, $"CMA-{lead.LastName}-{lead.Address.Replace(" ", "-")}.pdf");
        pdf.Generate(pdfPath, agent, lead, comps, cmaAnalysis, leadResearch, job.ReportType);
        job.PdfPath = pdfPath;

        // Step 6+7: Drive folder + upload PDF + Lead Brief Doc (non-blocking)
        Advance(job, CmaJobStatus.OrganizingDrive, onStatusChange);
        var agentEmail = agent.Identity?.Email ?? "";
        var folderPath = GwsService.BuildLeadFolderPath(lead.FullName, lead.FullAddress);

        try
        {
            await gws.CreateDriveFolderAsync(agentEmail, folderPath);
            var driveLink = await gws.UploadFileAsync(agentEmail, folderPath, pdfPath);
            job.DriveLink = driveLink;

            // Step 7: Create Lead Brief Google Doc
            var ownershipDuration = leadResearch?.PurchaseDate is not null
                ? LeadResearchService.CalculateOwnershipDuration(leadResearch.PurchaseDate.Value)
                : null;

            var equityRange = leadResearch?.EstimatedEquityLow.HasValue == true && leadResearch?.EstimatedEquityHigh.HasValue == true
                ? $"{leadResearch.EstimatedEquityLow.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}–{leadResearch.EstimatedEquityHigh.Value.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}"
                : null;

            var lotSize = leadResearch?.LotSize.HasValue == true
                ? $"{leadResearch.LotSize} {leadResearch.LotSizeUnit}"
                : null;

            var briefContent = GwsService.BuildLeadBriefContent(
                leadName: lead.FullName,
                address: lead.FullAddress,
                timeline: lead.Timeline,
                submittedAt: job.CreatedAt,
                occupation: leadResearch?.Occupation,
                employer: leadResearch?.Employer,
                purchaseDate: leadResearch?.PurchaseDate,
                purchasePrice: leadResearch?.PurchasePrice,
                ownershipDuration: ownershipDuration,
                equityRange: equityRange,
                lifeEvent: leadResearch?.LifeEventInsight,
                beds: lead.Beds,
                baths: lead.Baths,
                sqft: lead.Sqft,
                yearBuilt: leadResearch?.YearBuilt,
                lotSize: lotSize,
                taxAssessment: leadResearch?.TaxAssessment,
                annualTax: leadResearch?.AnnualPropertyTax,
                compCount: comps.Count,
                searchRadius: "1 mile",
                valueRange: $"{cmaAnalysis.ValueLow.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}–{cmaAnalysis.ValueHigh.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}",
                medianDom: cmaAnalysis.MedianDaysOnMarket,
                marketTrend: cmaAnalysis.MarketTrend,
                conversationStarters: cmaAnalysis.ConversationStarters,
                leadEmail: lead.Email,
                leadPhone: lead.Phone,
                pdfLink: driveLink);

            await gws.CreateDocAsync(agentEmail, folderPath, $"Lead Brief - {lead.FullName}", briefContent);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Drive/Doc operations failed for {LeadName} — continuing pipeline", lead.FullName);
        }

        // Step 8: Send email with PDF attachment
        Advance(job, CmaJobStatus.SendingEmail, onStatusChange);
        try
        {
            var emailBody = BuildEmailBody(agent, lead, cmaAnalysis);
            var subject = $"Your Complimentary Home Value Report — {lead.FullAddress}";
            await gws.SendEmailAsync(agentEmail, lead.Email, subject, emailBody, pdfPath);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Email send failed for {LeadEmail} — continuing pipeline", lead.Email);
        }

        // Step 9: Log to tracking sheet
        Advance(job, CmaJobStatus.Logging, onStatusChange);
        try
        {
            var spreadsheetId = agent.Integrations?.FormHandlerId ?? "";
            if (!string.IsNullOrWhiteSpace(spreadsheetId))
            {
                var row = new List<string>
                {
                    job.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    lead.FullName,
                    lead.FullAddress,
                    lead.Email,
                    lead.Phone,
                    lead.Timeline,
                    job.ReportType.ToString(),
                    comps.Count.ToString(),
                    cmaAnalysis.ValueMid.ToString("C0", CultureInfo.GetCultureInfo("en-US")),
                    job.DriveLink ?? "",
                    "Complete"
                };
                await gws.AppendSheetRowAsync(agentEmail, spreadsheetId, row);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Sheet logging failed — continuing pipeline");
        }

        // Mark complete
        Advance(job, CmaJobStatus.Complete, onStatusChange);

        return job;
    }

    private static void Advance(CmaJob job, CmaJobStatus status, Action<CmaJobStatus> onStatusChange)
    {
        job.AdvanceTo(status);
        onStatusChange(status);
    }

    private static Guid ToGuid(string value) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static string BuildEmailBody(AgentConfig agent, Lead lead, CmaAnalysis analysis)
    {
        var agentName = agent.Identity?.Name ?? "Your Real Estate Professional";
        var agentPhone = agent.Identity?.Phone ?? "";
        var agentEmail = agent.Identity?.Email ?? "";
        var brokerage = agent.Identity?.Brokerage ?? "";
        var firstName = lead.FirstName;

        return $"""
            Hi {firstName},

            Thank you for your interest in understanding the current market value of your home at {lead.FullAddress}. I've prepared a personalized Comparative Market Analysis just for you.

            Based on recent comparable sales in your area, I estimate your home's value to be in the range of {analysis.ValueLow.ToString("C0", CultureInfo.GetCultureInfo("en-US"))} to {analysis.ValueHigh.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}.

            {analysis.MarketNarrative}

            Your full CMA report is attached to this email. It includes detailed comparable sales data, market trends, and a pricing recommendation tailored to your property.

            I'd love to schedule a quick call to walk you through the report and answer any questions. Feel free to reach me at {agentPhone} or simply reply to this email.

            Best regards,
            {agentName}
            {brokerage}
            {agentPhone}
            {agentEmail}
            """;
    }
}
