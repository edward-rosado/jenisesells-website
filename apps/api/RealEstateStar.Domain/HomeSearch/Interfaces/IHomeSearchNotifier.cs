using RealEstateStar.Domain.Leads.Models;
namespace RealEstateStar.Domain.HomeSearch.Interfaces;

public interface IHomeSearchNotifier
{
    /// <summary>
    /// Sends curated listings to the buyer and stores the communication in Google Drive.
    /// </summary>
    Task NotifyBuyerAsync(string agentId, Lead lead, List<Listing> listings, string correlationId, CancellationToken ct);
}
