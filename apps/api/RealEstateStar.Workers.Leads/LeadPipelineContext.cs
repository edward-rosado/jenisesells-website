using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Workers.Shared.Context;

namespace RealEstateStar.Workers.Leads;

public class LeadPipelineContext : PipelineContext<Lead>
{
    // Step name constants
    public const string StepEnrich = "enrich";
    public const string StepDraftEmail = "draft-email";
    public const string StepNotify = "notify";

    // TODO: Pipeline redesign — LeadEnrichment removed in Phase 1.5; Enrichment step replaced in Phase 2/3/4
    // Enrichment property removed; Score and email draft properties retained for compatibility

    public LeadScore? Score
    {
        get => Get<LeadScore>("score");
        set { if (value is not null) Set("score", value); }
    }

    public string? EmailDraftSubject
    {
        get => Data.TryGetValue("email-draft-subject", out var v) ? v as string : null;
        set { if (value is not null) Data["email-draft-subject"] = value; }
    }

    public string? EmailDraftBody
    {
        get => Data.TryGetValue("email-draft-body", out var v) ? v as string : null;
        set { if (value is not null) Data["email-draft-body"] = value; }
    }
}
