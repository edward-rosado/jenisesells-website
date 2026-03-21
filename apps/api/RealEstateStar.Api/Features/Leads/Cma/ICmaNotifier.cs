namespace RealEstateStar.Api.Features.Leads.Cma;

public interface ICmaNotifier
{
    /// <summary>
    /// Sends the CMA report to the seller and stores the communication in Google Drive.
    /// </summary>
    Task NotifySellerAsync(string agentId, Lead lead, string pdfPath, CmaAnalysis analysis, string correlationId, CancellationToken ct);
}
