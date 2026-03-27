using RealEstateStar.Domain.Leads.Models;

namespace RealEstateStar.Domain.Privacy;

// TODO: Pipeline redesign — LeadEnrichment removed in Phase 1.5; export model updated in Phase 2/3/4
public record LeadExportData(
    Lead? Profile,
    List<MarketingConsent> ConsentHistory);
