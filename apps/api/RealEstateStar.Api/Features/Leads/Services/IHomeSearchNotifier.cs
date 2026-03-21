namespace RealEstateStar.Api.Features.Leads.Services;

public interface IHomeSearchNotifier
{
    /// <summary>
    /// Sends curated listings to the buyer and stores the communication in Google Drive.
    /// </summary>
    Task NotifyBuyerAsync(string agentId, Lead lead, List<Listing> listings, string correlationId, CancellationToken ct);
}
