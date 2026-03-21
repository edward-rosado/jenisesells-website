namespace RealEstateStar.Domain.Leads.Models;

/// <summary>
/// Lightweight DTO used by MultiChannelLeadNotifier to dispatch notifications
/// across channels (WhatsApp, email, etc.) without depending on the full CMA Lead model.
/// </summary>
public record LeadNotification(
    string Name,
    string Phone,
    string Email,
    string Interest,
    string Area);
