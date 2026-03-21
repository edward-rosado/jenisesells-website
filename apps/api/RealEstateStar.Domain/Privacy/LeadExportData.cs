using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Domain.Privacy;

public record LeadExportData(
    Lead? Profile,
    List<MarketingConsent> ConsentHistory,
    LeadEnrichment? Enrichment);
