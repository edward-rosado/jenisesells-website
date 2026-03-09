namespace RealEstateStar.Api.Models;

public static class StatusMessages
{
    public static string Get(CmaJobStatus status) => status switch
    {
        CmaJobStatus.Parsing => "Received your property details",
        CmaJobStatus.SearchingComps => "Searching MLS databases...",
        CmaJobStatus.ResearchingLead => "Researching property records...",
        CmaJobStatus.Analyzing => "Analyzing market trends...",
        CmaJobStatus.GeneratingPdf => "Generating your personalized report...",
        CmaJobStatus.OrganizingDrive => "Organizing documents...",
        CmaJobStatus.SendingEmail => "Sending report to your email...",
        CmaJobStatus.Logging => "Finalizing...",
        CmaJobStatus.Complete => "Your report has been sent to your email!",
        CmaJobStatus.Failed => "An error occurred while processing your report.",
        _ => "Processing..."
    };
}
