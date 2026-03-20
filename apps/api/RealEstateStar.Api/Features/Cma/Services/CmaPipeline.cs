using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using RealEstateStar.Api.Diagnostics;
using RealEstateStar.Api.Common;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services.Analysis;
using RealEstateStar.Api.Features.Cma.Services.Comps;
using RealEstateStar.Api.Services.Gws;
using RealEstateStar.Api.Features.Cma.Services.Pdf;
using RealEstateStar.Api.Features.Cma.Services.Research;
using RealEstateStar.Api.Services;

namespace RealEstateStar.Api.Features.Cma.Services;

public class CmaPipeline(
    IAgentConfigService agentConfig,
    CompAggregator compAggregator,
    ILeadResearchService research,
    IAnalysisService analysis,
    ICmaPdfGenerator pdf,
    IGwsService gws,
    ILogger<CmaPipeline>? logger = null) : ICmaPipeline
{
    public async Task ExecuteAsync(CmaJob job, string agentId, Lead lead, Func<CmaJobStatus, Task> onStatusChange, CancellationToken ct)
    {
        using var pipelineActivity = CmaDiagnostics.Source.StartActivity("CmaPipeline.Execute");
        pipelineActivity?.SetTag("cma.job.id", job.Id.ToString());
        pipelineActivity?.SetTag("cma.agent.id", agentId);
        pipelineActivity?.SetTag("cma.address.city", lead.City);
        pipelineActivity?.SetTag("cma.address.state", lead.State);

        var agentTag = new KeyValuePair<string, object?>("agent.id", agentId);

        var pipelineSw = Stopwatch.StartNew();
        var stepSw = new Stopwatch();

        try
        {
        // Step 1: Load agent config
        AgentConfig? agent;
        {
            using var stepActivity = CmaDiagnostics.Source.StartActivity("CmaPipeline.LoadAgentConfig");
            stepActivity?.SetTag("cma.step", "LoadAgentConfig");
            stepSw.Restart();
            agent = await agentConfig.GetAgentAsync(agentId, ct);
            stepSw.Stop();
            RecordStepDuration("LoadAgentConfig", stepSw.ElapsedMilliseconds);
            logger?.LogInformation("Pipeline step {Step} completed in {ElapsedMs}ms for job {JobId}",
                "LoadAgentConfig", stepSw.ElapsedMilliseconds, job.Id);
        }

        if (agent is null)
            throw new InvalidOperationException($"Agent configuration not found for '{agentId}'");

        await AdvanceAsync(job, CmaJobStatus.SearchingComps, onStatusChange);

        // Steps 2+3 in parallel: fetch comps + research lead
        List<Comp> comps;
        LeadResearch? leadResearch;
        {
            using var stepActivity = CmaDiagnostics.Source.StartActivity("CmaPipeline.SearchingComps");
            stepActivity?.SetTag("cma.step", "SearchingComps+Research");
            stepSw.Restart();
            var compSearchRequest = new CompSearchRequest
            {
                Address = lead.Address,
                City = lead.City,
                State = lead.State,
                Zip = lead.Zip,
                Beds = lead.Beds,
                Baths = lead.Baths,
                SqFt = lead.Sqft
            };
            var compsTask = compAggregator.FetchCompsAsync(compSearchRequest, ct);
            var researchTask = research.ResearchAsync(lead, ct);

            await Task.WhenAll(compsTask, researchTask);

            comps = await compsTask;
            leadResearch = await researchTask;
            stepSw.Stop();
            stepActivity?.SetTag("cma.comps.count", comps.Count);
            RecordStepDuration("SearchingComps+Research", stepSw.ElapsedMilliseconds);
            logger?.LogInformation("Pipeline step {Step} completed in {ElapsedMs}ms for job {JobId}",
                "SearchingComps+Research", stepSw.ElapsedMilliseconds, job.Id);
        }

        foreach (var comp in comps)
            job.Comps.Add(comp);
        job.LeadResearch = leadResearch;

        // Step 4: Claude analysis
        await AdvanceAsync(job, CmaJobStatus.Analyzing, onStatusChange);
        CmaAnalysis cmaAnalysis;
        {
            using var stepActivity = CmaDiagnostics.Source.StartActivity("CmaPipeline.Analyzing");
            stepActivity?.SetTag("cma.step", "Analyzing");
            stepSw.Restart();
            cmaAnalysis = await analysis.AnalyzeAsync(lead, comps, leadResearch, job.ReportType, ct);
            stepSw.Stop();
            RecordStepDuration("Analyzing", stepSw.ElapsedMilliseconds);
            logger?.LogInformation("Pipeline step {Step} completed in {ElapsedMs}ms for job {JobId}",
                "Analyzing", stepSw.ElapsedMilliseconds, job.Id);
        }
        job.Analysis = cmaAnalysis;

        // Step 5: Generate PDF
        await AdvanceAsync(job, CmaJobStatus.GeneratingPdf, onStatusChange);
        string pdfPath;
        {
            using var stepActivity = CmaDiagnostics.Source.StartActivity("CmaPipeline.GeneratingPdf");
            stepActivity?.SetTag("cma.step", "GeneratingPdf");
            stepSw.Restart();
            var tempDir = Path.Combine(Path.GetTempPath(), "cma", job.Id.ToString());
            Directory.CreateDirectory(tempDir);
            pdfPath = Path.Combine(tempDir, $"CMA-{SanitizeFileName(lead.LastName)}-{SanitizeFileName(lead.Address)}.pdf");
            pdf.Generate(new PdfGenerationRequest
            {
                OutputPath = pdfPath,
                Agent = agent,
                Lead = lead,
                Comps = comps,
                Analysis = cmaAnalysis,
                Research = leadResearch,
                ReportType = job.ReportType
            }, ct);
            stepSw.Stop();
            RecordStepDuration("GeneratingPdf", stepSw.ElapsedMilliseconds);
            logger?.LogInformation("Pipeline step {Step} completed in {ElapsedMs}ms for job {JobId}",
                "GeneratingPdf", stepSw.ElapsedMilliseconds, job.Id);
        }
        job.PdfPath = pdfPath;

        // Step 6+7: Drive folder + upload PDF + Lead Brief Doc (non-blocking)
        await AdvanceAsync(job, CmaJobStatus.OrganizingDrive, onStatusChange);
        var agentEmail = agent.Identity?.Email ?? "";
        var folderPath = GwsService.BuildLeadFolderPath(lead.FullName, lead.FullAddress);

        {
            using var stepActivity = CmaDiagnostics.Source.StartActivity("CmaPipeline.OrganizingDrive");
            stepActivity?.SetTag("cma.step", "OrganizingDrive");
            stepSw.Restart();
            try
            {
                await gws.CreateDriveFolderAsync(agentEmail, folderPath, ct);
                var driveLink = await gws.UploadFileAsync(agentEmail, folderPath, pdfPath, ct);
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

                var briefContent = GwsService.BuildLeadBriefContent(new LeadBriefData
                {
                    LeadName = lead.FullName,
                    Address = lead.FullAddress,
                    Timeline = lead.Timeline,
                    SubmittedAt = job.CreatedAt,
                    Occupation = leadResearch?.Occupation,
                    Employer = leadResearch?.Employer,
                    PurchaseDate = leadResearch?.PurchaseDate,
                    PurchasePrice = leadResearch?.PurchasePrice,
                    OwnershipDuration = ownershipDuration,
                    EquityRange = equityRange,
                    LifeEvent = leadResearch?.LifeEventInsight,
                    Beds = lead.Beds,
                    Baths = lead.Baths,
                    Sqft = lead.Sqft,
                    YearBuilt = leadResearch?.YearBuilt,
                    LotSize = lotSize,
                    TaxAssessment = leadResearch?.TaxAssessment,
                    AnnualTax = leadResearch?.AnnualPropertyTax,
                    CompCount = comps.Count,
                    SearchRadius = "1 mile",
                    ValueRange = $"{cmaAnalysis.ValueLow.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}–{cmaAnalysis.ValueHigh.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}",
                    MedianDom = cmaAnalysis.MedianDaysOnMarket,
                    MarketTrend = cmaAnalysis.MarketTrend,
                    ConversationStarters = cmaAnalysis.ConversationStarters,
                    LeadEmail = lead.Email,
                    LeadPhone = lead.Phone,
                    PdfLink = driveLink
                });

                await gws.CreateDocAsync(agentEmail, folderPath, $"Lead Brief - {lead.FullName}", briefContent, ct);
            }
            catch (Exception ex)
            {
                stepActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger?.LogWarning(ex, "Drive/Doc operations failed for agent {AgentId}, job {JobId}, lead {LeadName} — continuing pipeline",
                    agentId, job.Id, lead.FullName);
            }
            stepSw.Stop();
            RecordStepDuration("OrganizingDrive", stepSw.ElapsedMilliseconds);
            logger?.LogInformation("Pipeline step {Step} completed in {ElapsedMs}ms for job {JobId}",
                "OrganizingDrive", stepSw.ElapsedMilliseconds, job.Id);
        }

        // Step 8: Send email with PDF attachment
        await AdvanceAsync(job, CmaJobStatus.SendingEmail, onStatusChange);
        {
            using var stepActivity = CmaDiagnostics.Source.StartActivity("CmaPipeline.SendingEmail");
            stepActivity?.SetTag("cma.step", "SendingEmail");
            stepSw.Restart();
            try
            {
                var emailBody = BuildEmailBody(agent, lead, cmaAnalysis);
                var subject = $"Your Complimentary Home Value Report — {lead.FullAddress}";
                await gws.SendEmailAsync(agentEmail, lead.Email, subject, emailBody, pdfPath, ct);
            }
            catch (Exception ex)
            {
                stepActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger?.LogWarning(ex, "Email send failed for agent {AgentId}, job {JobId}, lead {LeadEmail} — continuing pipeline",
                    agentId, job.Id, lead.Email);
            }
            stepSw.Stop();
            RecordStepDuration("SendingEmail", stepSw.ElapsedMilliseconds);
            logger?.LogInformation("Pipeline step {Step} completed in {ElapsedMs}ms for job {JobId}",
                "SendingEmail", stepSw.ElapsedMilliseconds, job.Id);
        }

        // Step 9: Log to tracking sheet
        await AdvanceAsync(job, CmaJobStatus.Logging, onStatusChange);
        {
            using var stepActivity = CmaDiagnostics.Source.StartActivity("CmaPipeline.Logging");
            stepActivity?.SetTag("cma.step", "Logging");
            stepSw.Restart();
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
                    await gws.AppendSheetRowAsync(agentEmail, spreadsheetId, row, ct);
                }
            }
            catch (Exception ex)
            {
                stepActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger?.LogWarning(ex, "Sheet logging failed for agent {AgentId}, job {JobId} — continuing pipeline",
                    agentId, job.Id);
            }
            stepSw.Stop();
            RecordStepDuration("Logging", stepSw.ElapsedMilliseconds);
            logger?.LogInformation("Pipeline step {Step} completed in {ElapsedMs}ms for job {JobId}",
                "Logging", stepSw.ElapsedMilliseconds, job.Id);
        }

        // Mark complete
        await AdvanceAsync(job, CmaJobStatus.Complete, onStatusChange);

        pipelineSw.Stop();
        CmaDiagnostics.CmaCompleted.Add(1, agentTag);
        CmaDiagnostics.CmaDuration.Record(pipelineSw.ElapsedMilliseconds, agentTag);
        pipelineActivity?.SetStatus(ActivityStatusCode.Ok);
        logger?.LogInformation("Pipeline completed in {TotalElapsedMs}ms for agent {AgentId}, job {JobId}",
            pipelineSw.ElapsedMilliseconds, agentId, job.Id);
        }
        catch (Exception ex)
        {
            pipelineActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            CmaDiagnostics.CmaFailed.Add(1, agentTag);
            throw;
        }
    }

    private static void RecordStepDuration(string stepName, long elapsedMs) =>
        CmaDiagnostics.CmaStepDuration.Record(elapsedMs, new KeyValuePair<string, object?>("step", stepName));

    private static async Task AdvanceAsync(CmaJob job, CmaJobStatus status, Func<CmaJobStatus, Task> onStatusChange)
    {
        job.AdvanceTo(status);
        await onStatusChange(status);
    }

    private static string SanitizeFileName(string input) =>
        Path.GetInvalidFileNameChars()
            .Aggregate(input.Replace(" ", "-"), (current, c) => current.Replace(c, '_'));

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
