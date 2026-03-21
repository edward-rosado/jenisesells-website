using RealEstateStar.Api.Diagnostics;

namespace RealEstateStar.Api.Tests.Diagnostics;

public class LeadDiagnosticsTests
{
    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        Assert.Equal("RealEstateStar.Leads", LeadDiagnostics.ActivitySource.Name);
    }

    [Fact]
    public void LeadsReceived_Counter_Exists()
    {
        Assert.NotNull(LeadDiagnostics.LeadsReceived);
        Assert.Equal("leads.received", LeadDiagnostics.LeadsReceived.Name);
    }

    [Fact]
    public void LeadsEnriched_Counter_Exists()
    {
        Assert.NotNull(LeadDiagnostics.LeadsEnriched);
        Assert.Equal("leads.enriched", LeadDiagnostics.LeadsEnriched.Name);
    }

    [Fact]
    public void LeadsEnrichmentFailed_Counter_Exists()
    {
        Assert.NotNull(LeadDiagnostics.LeadsEnrichmentFailed);
        Assert.Equal("leads.enrichment_failed", LeadDiagnostics.LeadsEnrichmentFailed.Name);
    }

    [Fact]
    public void LeadsNotificationSent_Counter_Exists()
    {
        Assert.NotNull(LeadDiagnostics.LeadsNotificationSent);
        Assert.Equal("leads.notification_sent", LeadDiagnostics.LeadsNotificationSent.Name);
    }

    [Fact]
    public void LeadsNotificationFailed_Counter_Exists()
    {
        Assert.NotNull(LeadDiagnostics.LeadsNotificationFailed);
        Assert.Equal("leads.notification_failed", LeadDiagnostics.LeadsNotificationFailed.Name);
    }

    [Fact]
    public void LeadsDeleted_Counter_Exists()
    {
        Assert.NotNull(LeadDiagnostics.LeadsDeleted);
        Assert.Equal("leads.deleted", LeadDiagnostics.LeadsDeleted.Name);
    }

    [Fact]
    public void EnrichmentDuration_Histogram_Exists()
    {
        Assert.NotNull(LeadDiagnostics.EnrichmentDuration);
        Assert.Equal("leads.enrichment_duration_ms", LeadDiagnostics.EnrichmentDuration.Name);
    }

    [Fact]
    public void NotificationDuration_Histogram_Exists()
    {
        Assert.NotNull(LeadDiagnostics.NotificationDuration);
        Assert.Equal("leads.notification_duration_ms", LeadDiagnostics.NotificationDuration.Name);
    }

    [Fact]
    public void HomeSearchDuration_Histogram_Exists()
    {
        Assert.NotNull(LeadDiagnostics.HomeSearchDuration);
        Assert.Equal("leads.home_search_duration_ms", LeadDiagnostics.HomeSearchDuration.Name);
    }

    [Fact]
    public void TotalPipelineDuration_Histogram_Exists()
    {
        Assert.NotNull(LeadDiagnostics.TotalPipelineDuration);
        Assert.Equal("leads.total_pipeline_duration_ms", LeadDiagnostics.TotalPipelineDuration.Name);
    }

    [Fact]
    public void LlmTokensInput_Counter_Exists()
    {
        Assert.NotNull(LeadDiagnostics.LlmTokensInput);
        Assert.Equal("leads.llm_tokens.input", LeadDiagnostics.LlmTokensInput.Name);
    }

    [Fact]
    public void LlmTokensOutput_Counter_Exists()
    {
        Assert.NotNull(LeadDiagnostics.LlmTokensOutput);
        Assert.Equal("leads.llm_tokens.output", LeadDiagnostics.LlmTokensOutput.Name);
    }

    [Fact]
    public void LlmCostUsd_Counter_Exists()
    {
        Assert.NotNull(LeadDiagnostics.LlmCostUsd);
        Assert.Equal("leads.llm_cost_usd", LeadDiagnostics.LlmCostUsd.Name);
    }
}
