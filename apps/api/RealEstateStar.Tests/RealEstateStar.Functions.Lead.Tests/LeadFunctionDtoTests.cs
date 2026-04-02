using System.Text.Json;
using FluentAssertions;
using RealEstateStar.Functions.Lead.Models;
using AgentNotificationConfig = RealEstateStar.Domain.Leads.Models.AgentNotificationConfig;
using CmaWorkerResult = RealEstateStar.Domain.Leads.Models.CmaWorkerResult;
using HomeSearchWorkerResult = RealEstateStar.Domain.Leads.Models.HomeSearchWorkerResult;
using CompSummary = RealEstateStar.Domain.Leads.Models.CompSummary;
using ListingSummary = RealEstateStar.Domain.Leads.Models.ListingSummary;
using LeadScore = RealEstateStar.Domain.Leads.Models.LeadScore;
using ScoreFactor = RealEstateStar.Domain.Leads.Models.ScoreFactor;

namespace RealEstateStar.Functions.Lead.Tests;

/// <summary>
/// Serialization round-trip tests for all Lead pipeline DTOs.
/// Verifies JSON property names are stable and values survive
/// JSON → deserialize → re-serialize without loss.
/// </summary>
public class LeadFunctionDtoTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    private static T RoundTrip<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, Options);
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }

    [Fact]
    public void LeadOrchestrationMessage_roundtrip_preserves_all_fields()
    {
        var original = new LeadOrchestrationMessage
        {
            AgentId = "agent-123",
            LeadId = "lead-456",
            CorrelationId = "corr-789"
        };

        var rt = RoundTrip(original);

        rt.AgentId.Should().Be(original.AgentId);
        rt.LeadId.Should().Be(original.LeadId);
        rt.CorrelationId.Should().Be(original.CorrelationId);
    }

    [Fact]
    public void LeadOrchestratorInput_roundtrip_preserves_all_fields()
    {
        var original = new LeadOrchestratorInput
        {
            AgentId = "agent-123",
            LeadId = "lead-456",
            CorrelationId = "corr-789",
            ShouldRunCma = true,
            ShouldRunHomeSearch = false,
            CmaInputHash = "abc123",
            HsInputHash = "def456"
        };

        var rt = RoundTrip(original);

        rt.AgentId.Should().Be(original.AgentId);
        rt.LeadId.Should().Be(original.LeadId);
        rt.ShouldRunCma.Should().BeTrue();
        rt.ShouldRunHomeSearch.Should().BeFalse();
        rt.CmaInputHash.Should().Be("abc123");
        rt.HsInputHash.Should().Be("def456");
    }

    [Fact]
    public void LoadAgentConfigOutput_found_roundtrip_preserves_nested_config()
    {
        var config = new AgentNotificationConfig
        {
            AgentId = "agent-123",
            Handle = "jenise-buckalew",
            Name = "Jenise Buckalew",
            FirstName = "Jenise",
            Email = "jenise@example.com",
            Phone = "555-1234",
            LicenseNumber = "NJ12345",
            BrokerageName = "RE/MAX",
            PrimaryColor = "#004b8d",
            AccentColor = "#ffcc00",
            State = "NJ"
        };

        var original = new LoadAgentConfigOutput { Found = true, AgentNotificationConfig = config };
        var rt = RoundTrip(original);

        rt.Found.Should().BeTrue();
        rt.AgentNotificationConfig.Should().NotBeNull();
        rt.AgentNotificationConfig!.AgentId.Should().Be("agent-123");
        rt.AgentNotificationConfig.Handle.Should().Be("jenise-buckalew");
        rt.AgentNotificationConfig.State.Should().Be("NJ");
    }

    [Fact]
    public void LoadAgentConfigOutput_not_found_roundtrip()
    {
        var original = new LoadAgentConfigOutput { Found = false };
        var rt = RoundTrip(original);

        rt.Found.Should().BeFalse();
        rt.AgentNotificationConfig.Should().BeNull();
    }

    [Fact]
    public void ScoreLeadOutput_roundtrip_preserves_score()
    {
        var score = new LeadScore
        {
            OverallScore = 85,
            Factors = [new ScoreFactor { Category = "Contact", Score = 10, Weight = 1.0m, Explanation = "Email provided" }],
            Explanation = "High-quality lead"
        };

        var original = new ScoreLeadOutput { Score = score };
        var rt = RoundTrip(original);

        rt.Score.OverallScore.Should().Be(85);
        rt.Score.Bucket.Should().Be("Hot"); // computed: >= 70 → Hot
        rt.Score.Explanation.Should().Be("High-quality lead");
    }

    [Fact]
    public void CheckContentCacheOutput_cache_hit_roundtrip()
    {
        var cmaResult = new CmaWorkerResult(
            "lead-1", true, null,
            450000m, 430000m, 470000m,
            [new CompSummary("123 Main St", 440000m, 3, 2m, 1800, 15, 0.2, DateOnly.FromDateTime(DateTime.UtcNow))],
            "Strong seller's market");

        var original = new CheckContentCacheOutput
        {
            CmaCacheHit = true,
            HsCacheHit = false,
            CachedCmaResult = cmaResult,
            CachedHsResult = null
        };

        var rt = RoundTrip(original);

        rt.CmaCacheHit.Should().BeTrue();
        rt.HsCacheHit.Should().BeFalse();
        rt.CachedCmaResult.Should().NotBeNull();
        rt.CachedCmaResult!.EstimatedValue.Should().Be(450000m);
        rt.CachedCmaResult.Success.Should().BeTrue();
        rt.CachedHsResult.Should().BeNull();
    }

    [Fact]
    public void CmaFunctionInput_roundtrip()
    {
        var config = BuildMinimalAgentConfig();
        var original = new CmaFunctionInput
        {
            AgentId = "a1",
            LeadId = "l1",
            CorrelationId = "c1",
            AgentNotificationConfig = config
        };

        var rt = RoundTrip(original);

        rt.AgentId.Should().Be("a1");
        rt.AgentNotificationConfig.AgentId.Should().Be(config.AgentId);
    }

    [Fact]
    public void CmaFunctionOutput_roundtrip_success()
    {
        var result = new CmaWorkerResult("l1", true, null, 500000m, 480000m, 520000m, [], "Great market");
        var original = new CmaFunctionOutput { Result = result };
        var rt = RoundTrip(original);

        rt.Result.Success.Should().BeTrue();
        rt.Result.EstimatedValue.Should().Be(500000m);
        rt.Result.MarketAnalysis.Should().Be("Great market");
    }

    [Fact]
    public void CmaFunctionOutput_roundtrip_failure()
    {
        var result = new CmaWorkerResult("l1", false, "No comps", null, null, null, null, null);
        var original = new CmaFunctionOutput { Result = result };
        var rt = RoundTrip(original);

        rt.Result.Success.Should().BeFalse();
        rt.Result.Error.Should().Be("No comps");
        rt.Result.EstimatedValue.Should().BeNull();
    }

    [Fact]
    public void HomeSearchFunctionOutput_roundtrip_success()
    {
        var result = new HomeSearchWorkerResult("l1", true, null,
            [new ListingSummary("100 Oak Ave", 380000m, 3, 2m, 1600, "Active", null)],
            "Active buyer market");
        var original = new HomeSearchFunctionOutput { Result = result };
        var rt = RoundTrip(original);

        rt.Result.Success.Should().BeTrue();
        rt.Result.Listings.Should().HaveCount(1);
        rt.Result.Listings![0].Address.Should().Be("100 Oak Ave");
    }

    [Fact]
    public void HomeSearchFunctionOutput_roundtrip_failure()
    {
        var result = new HomeSearchWorkerResult("l1", false, "Provider error", null, null);
        var original = new HomeSearchFunctionOutput { Result = result };
        var rt = RoundTrip(original);

        rt.Result.Success.Should().BeFalse();
        rt.Result.Error.Should().Be("Provider error");
    }

    [Fact]
    public void GeneratePdfInput_roundtrip()
    {
        var cmaResult = new CmaWorkerResult("l1", true, null, 500000m, null, null, null, null);
        var original = new GeneratePdfInput
        {
            AgentId = "a1",
            LeadId = "l1",
            CorrelationId = "c1",
            CmaResult = cmaResult
        };

        var rt = RoundTrip(original);

        rt.CmaResult.EstimatedValue.Should().Be(500000m);
    }

    [Fact]
    public void GeneratePdfOutput_roundtrip()
    {
        var original = new GeneratePdfOutput { PdfStoragePath = "leads/john-doe/CMA.pdf.b64" };
        var rt = RoundTrip(original);
        rt.PdfStoragePath.Should().Be("leads/john-doe/CMA.pdf.b64");
    }

    [Fact]
    public void DraftLeadEmailInput_roundtrip_with_all_results()
    {
        var config = BuildMinimalAgentConfig();
        var score = new LeadScore { OverallScore = 75, Factors = [], Explanation = "Good" };
        var cma = new CmaWorkerResult("l1", true, null, 500000m, null, null, null, null);
        var hs = new HomeSearchWorkerResult("l1", true, null, [], null);

        var original = new DraftLeadEmailInput
        {
            AgentId = "a1",
            LeadId = "l1",
            CorrelationId = "c1",
            AgentNotificationConfig = config,
            Score = score,
            CmaResult = cma,
            HsResult = hs
        };

        var rt = RoundTrip(original);

        rt.Score.OverallScore.Should().Be(75);
        rt.CmaResult.Should().NotBeNull();
        rt.HsResult.Should().NotBeNull();
    }

    [Fact]
    public void DraftLeadEmailOutput_roundtrip_with_pdf_attachment()
    {
        var original = new DraftLeadEmailOutput
        {
            Subject = "Your CMA Report is Ready",
            HtmlBody = "<p>Hi John</p>",
            PdfAttachmentPath = "leads/john-doe/CMA.pdf.b64"
        };

        var rt = RoundTrip(original);

        rt.Subject.Should().Be("Your CMA Report is Ready");
        rt.HtmlBody.Should().Be("<p>Hi John</p>");
        rt.PdfAttachmentPath.Should().Be("leads/john-doe/CMA.pdf.b64");
    }

    [Fact]
    public void DraftLeadEmailOutput_roundtrip_without_pdf()
    {
        var original = new DraftLeadEmailOutput
        {
            Subject = "Hi John",
            HtmlBody = "<p>body</p>",
            PdfAttachmentPath = null
        };

        var rt = RoundTrip(original);
        rt.PdfAttachmentPath.Should().BeNull();
    }

    [Fact]
    public void PersistLeadResultsInput_roundtrip_full_pipeline()
    {
        var original = new PersistLeadResultsInput
        {
            AgentId = "a1",
            LeadId = "l1",
            CorrelationId = "c1",
            Score = new LeadScore { OverallScore = 80, Factors = [], Explanation = "x" },
            CmaResult = new CmaWorkerResult("l1", true, null, 500000m, 480000m, 520000m, [], "narrative"),
            HsResult = new HomeSearchWorkerResult("l1", true, null, [], null),
            PdfStoragePath = "leads/lead/cma.pdf.b64",
            EmailDraft = new DraftLeadEmailOutput { Subject = "Hi", HtmlBody = "<p>hi</p>", PdfAttachmentPath = null },
            EmailSent = true,
            AgentNotified = true,
            CmaInputHash = "cmahash",
            HsInputHash = "hshash"
        };

        var rt = RoundTrip(original);

        rt.EmailSent.Should().BeTrue();
        rt.AgentNotified.Should().BeTrue();
        rt.CmaInputHash.Should().Be("cmahash");
        rt.HsInputHash.Should().Be("hshash");
        rt.PdfStoragePath.Should().Be("leads/lead/cma.pdf.b64");
    }

    [Fact]
    public void PersistLeadResultsInput_roundtrip_partial_pipeline()
    {
        var original = new PersistLeadResultsInput
        {
            AgentId = "a1",
            LeadId = "l1",
            CorrelationId = "c1",
            Score = new LeadScore { OverallScore = 50, Factors = [], Explanation = "x" },
            CmaResult = null,
            HsResult = null,
            PdfStoragePath = null,
            EmailDraft = null,
            EmailSent = false,
            AgentNotified = false,
            CmaInputHash = "x",
            HsInputHash = "y"
        };

        var rt = RoundTrip(original);

        rt.CmaResult.Should().BeNull();
        rt.EmailSent.Should().BeFalse();
        rt.PdfStoragePath.Should().BeNull();
    }

    [Fact]
    public void UpdateContentCacheInput_roundtrip_both_results()
    {
        var original = new UpdateContentCacheInput
        {
            CmaInputHash = "cma_hash_1",
            HsInputHash = "hs_hash_1",
            CmaResult = new CmaWorkerResult("l1", true, null, 500000m, null, null, null, null),
            HsResult = new HomeSearchWorkerResult("l1", true, null, [], null),
            CorrelationId = "corr"
        };

        var rt = RoundTrip(original);

        rt.CmaInputHash.Should().Be("cma_hash_1");
        rt.HsInputHash.Should().Be("hs_hash_1");
        rt.CmaResult.Should().NotBeNull();
        rt.HsResult.Should().NotBeNull();
    }

    [Fact]
    public void UpdateContentCacheInput_roundtrip_null_results()
    {
        var original = new UpdateContentCacheInput
        {
            CmaInputHash = "h1",
            HsInputHash = "h2",
            CmaResult = null,
            HsResult = null,
            CorrelationId = "corr"
        };

        var rt = RoundTrip(original);

        rt.CmaResult.Should().BeNull();
        rt.HsResult.Should().BeNull();
    }

    [Fact]
    public void LeadOrchestrationMessage_json_uses_camelCase_property_names()
    {
        var msg = new LeadOrchestrationMessage { AgentId = "a", LeadId = "l", CorrelationId = "c" };
        var json = JsonSerializer.Serialize(msg, Options);

        json.Should().Contain("\"agentId\"");
        json.Should().Contain("\"leadId\"");
        json.Should().Contain("\"correlationId\"");
    }

    [Fact]
    public void LeadOrchestratorInput_json_uses_camelCase_routing_flags()
    {
        var input = new LeadOrchestratorInput
        {
            AgentId = "a", LeadId = "l", CorrelationId = "c",
            ShouldRunCma = true, ShouldRunHomeSearch = false,
            CmaInputHash = "h1", HsInputHash = "h2"
        };

        var json = JsonSerializer.Serialize(input, Options);

        json.Should().Contain("\"shouldRunCma\"");
        json.Should().Contain("\"shouldRunHomeSearch\"");
        json.Should().Contain("\"cmaInputHash\"");
        json.Should().Contain("\"hsInputHash\"");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static AgentNotificationConfig BuildMinimalAgentConfig() => new()
    {
        AgentId = "agent-test",
        Handle = "test-agent",
        Name = "Test Agent",
        FirstName = "Test",
        Email = "test@example.com",
        Phone = "555-0000",
        LicenseNumber = "LIC-001",
        BrokerageName = "Test Brokerage",
        PrimaryColor = "#000",
        AccentColor = "#fff",
        State = "NJ"
    };
}
