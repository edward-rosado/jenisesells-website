namespace RealEstateStar.Api.Features.Leads;

public static class LeadPaths
{
    public const string Root = "Real Estate Star";
    public const string LeadsFolder = "Real Estate Star/1 - Leads";
    public const string ConsentLogSheet = "Real Estate Star/Marketing Consent Log";
    public const string DeletionLogSheet = "Real Estate Star/Deletion Audit Log";

    public static string LeadFolder(string name)
        => $"{LeadsFolder}/{name}";

    public static string LeadFile(string name)
        => $"{LeadFolder(name)}/Lead Profile.md";

    public static string EnrichmentFile(string name)
        => $"{LeadFolder(name)}/Research & Insights.md";

    public static string HomeSearchFile(string name, DateTime date)
        => $"{LeadFolder(name)}/Home Search/{date:yyyy-MM-dd}-Home Search Results.md";

    public static string CmaFolder(string name, string address)
        => $"{LeadFolder(name)}/{address}";

    public static string DeletionAuditLogSheet(string agentId)
        => $"{Root}/{agentId}/Deletion Audit Log";
}
