namespace RealEstateStar.Api.Models;

public enum CmaJobStatus
{
    Parsing = 0,
    SearchingComps = 1,
    ResearchingLead = 2,
    Analyzing = 3,
    GeneratingPdf = 4,
    OrganizingDrive = 5,
    SendingEmail = 6,
    Logging = 7,
    Complete = 8
}

public enum ReportType
{
    Lean,
    Standard,
    Comprehensive
}

public class CmaJob
{
    public Guid Id { get; init; }
    public Guid AgentId { get; init; }
    public required Lead Lead { get; init; }
    public CmaJobStatus Status { get; private set; }
    public int Step { get; private set; }
    public int TotalSteps => 9;
    public ReportType ReportType { get; init; }
    public List<Comp> Comps { get; init; } = [];
    public LeadResearch? LeadResearch { get; set; }
    public CmaAnalysis? Analysis { get; set; }
    public string? PdfPath { get; set; }
    public string? DriveLink { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; set; }

    public static CmaJob Create(Guid agentId, Lead lead) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = agentId,
        Lead = lead,
        Status = CmaJobStatus.Parsing,
        Step = 0,
        ReportType = GetReportType(lead.Timeline),
        CreatedAt = DateTime.UtcNow
    };

    public void AdvanceTo(CmaJobStatus status)
    {
        Status = status;
        Step = (int)status;

        if (status == CmaJobStatus.Complete)
            CompletedAt = DateTime.UtcNow;
    }

    public static ReportType GetReportType(string timeline) => timeline switch
    {
        "Just curious" => ReportType.Lean,
        "6-12 months" => ReportType.Standard,
        "3-6 months" => ReportType.Standard,
        "1-3 months" => ReportType.Comprehensive,
        "ASAP" => ReportType.Comprehensive,
        _ => ReportType.Standard
    };
}
