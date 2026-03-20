namespace RealEstateStar.Api.Features.Leads;

/// <summary>
/// Stub — will be replaced by the lead submission API plan.
/// Provides Drive folder path conventions for lead data.
/// </summary>
public static class LeadPaths
{
    public static string LeadFolder(string leadName) =>
        $"Real Estate Star/1 - Leads/{leadName}";
}
