using FluentAssertions;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Activities.Lead.ContactDetection.Tests;

public class ContactClassifierTests
{
    // ── DetermineStage ────────────────────────────────────────────────────

    [Fact]
    public void DetermineStage_returns_Lead_when_no_documents()
    {
        ContactClassifier.DetermineStage([]).Should().Be(PipelineStage.Lead);
    }

    [Fact]
    public void DetermineStage_returns_Lead_for_non_evidence_documents()
    {
        ContactClassifier.DetermineStage([DocumentType.Other, DocumentType.Cma, DocumentType.Inspection])
            .Should().Be(PipelineStage.Lead);
    }

    [Fact]
    public void DetermineStage_returns_ActiveClient_for_ListingAgreement()
    {
        ContactClassifier.DetermineStage([DocumentType.ListingAgreement])
            .Should().Be(PipelineStage.ActiveClient);
    }

    [Fact]
    public void DetermineStage_returns_ActiveClient_for_BuyerAgreement()
    {
        ContactClassifier.DetermineStage([DocumentType.BuyerAgreement])
            .Should().Be(PipelineStage.ActiveClient);
    }

    [Fact]
    public void DetermineStage_returns_UnderContract_for_PurchaseContract()
    {
        ContactClassifier.DetermineStage([DocumentType.PurchaseContract])
            .Should().Be(PipelineStage.UnderContract);
    }

    [Fact]
    public void DetermineStage_returns_Closed_for_ClosingStatement()
    {
        ContactClassifier.DetermineStage([DocumentType.ClosingStatement])
            .Should().Be(PipelineStage.Closed);
    }

    [Fact]
    public void DetermineStage_ClosingStatement_wins_over_PurchaseContract()
    {
        ContactClassifier.DetermineStage([DocumentType.PurchaseContract, DocumentType.ClosingStatement])
            .Should().Be(PipelineStage.Closed);
    }

    [Fact]
    public void DetermineStage_PurchaseContract_wins_over_ListingAgreement()
    {
        ContactClassifier.DetermineStage([DocumentType.ListingAgreement, DocumentType.PurchaseContract])
            .Should().Be(PipelineStage.UnderContract);
    }

    [Fact]
    public void DetermineStage_ClosingStatement_wins_over_all_others()
    {
        ContactClassifier.DetermineStage([
            DocumentType.ListingAgreement,
            DocumentType.BuyerAgreement,
            DocumentType.PurchaseContract,
            DocumentType.ClosingStatement
        ]).Should().Be(PipelineStage.Closed);
    }

    // ── ClassifyAndDedup ─────────────────────────────────────────────────

    private static ExtractedClient MakeClient(string name, string? email = null, string? phone = null,
        ContactRole role = ContactRole.Unknown) =>
        new(name, role, email, phone);

    private static DocumentReference MakeDoc(DocumentType type) =>
        new("file-1", "doc.pdf", type, DateTime.UtcNow);

    [Fact]
    public void ClassifyAndDedup_returns_empty_for_empty_input()
    {
        ContactClassifier.ClassifyAndDedup([]).Should().BeEmpty();
    }

    [Fact]
    public void ClassifyAndDedup_deduplicates_by_email_case_insensitive()
    {
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("John Smith", "john@example.com"), null),
            (MakeClient("John A. Smith", "JOHN@EXAMPLE.COM"), null),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("John Smith");
    }

    [Fact]
    public void ClassifyAndDedup_deduplicates_by_normalized_name_when_no_email()
    {
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("Jane Doe"), null),
            (MakeClient("jane doe"), null),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void ClassifyAndDedup_keeps_distinct_contacts_separate()
    {
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("Alice", "alice@example.com"), null),
            (MakeClient("Bob", "bob@example.com"), null),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void ClassifyAndDedup_assigns_stage_from_documents()
    {
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("Buyer One", "buyer@example.com"), MakeDoc(DocumentType.PurchaseContract)),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result[0].Stage.Should().Be(PipelineStage.UnderContract);
    }

    [Fact]
    public void ClassifyAndDedup_assigns_Closed_stage_for_ClosingStatement()
    {
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("Seller One", "seller@example.com"), MakeDoc(DocumentType.ClosingStatement)),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result[0].Stage.Should().Be(PipelineStage.Closed);
    }

    [Fact]
    public void ClassifyAndDedup_assigns_Lead_stage_when_no_documents()
    {
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("New Lead", "newlead@example.com"), null),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result[0].Stage.Should().Be(PipelineStage.Lead);
    }

    [Fact]
    public void ClassifyAndDedup_merges_documents_from_duplicate_entries()
    {
        var doc1 = MakeDoc(DocumentType.ListingAgreement);
        var doc2 = MakeDoc(DocumentType.ClosingStatement);

        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("Susan Brown", "susan@example.com"), doc1),
            (MakeClient("Susan Brown", "SUSAN@EXAMPLE.COM"), doc2),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result.Should().HaveCount(1);
        result[0].Documents.Should().HaveCount(2);
        result[0].Stage.Should().Be(PipelineStage.Closed, because: "ClosingStatement wins over ListingAgreement");
    }

    [Fact]
    public void ClassifyAndDedup_prefers_email_entry_as_primary_within_email_dedup_group()
    {
        // Two entries with the SAME email (case-insensitive) merge into one.
        // The resulting contact uses the email value.
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("Robert Davis", "rdavis@email.com"), null),
            (MakeClient("R. Davis", "RDAVIS@EMAIL.COM"), null),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("rdavis@email.com");
    }

    [Fact]
    public void ClassifyAndDedup_keeps_email_and_no_email_entries_separate_when_names_differ()
    {
        // No-email entry's key is name-based; email entry's key is email-based.
        // They don't merge unless their names are identical (normalized).
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("Robert Davis"), null),                     // key = name:robert davis
            (MakeClient("Robert Davis", "rdavis@email.com"), null), // key = email:rdavis@email.com
        };

        // Different keys → separate groups
        var result = ContactClassifier.ClassifyAndDedup(entries);
        result.Should().HaveCount(2);
    }

    [Fact]
    public void ClassifyAndDedup_prefers_specific_role_over_Unknown()
    {
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("Tom Wilson", "tom@example.com", role: ContactRole.Unknown), null),
            (MakeClient("Tom Wilson", "tom@example.com", role: ContactRole.Buyer), null),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result.Should().HaveCount(1);
        result[0].Role.Should().Be(ContactRole.Buyer);
    }

    [Fact]
    public void ClassifyAndDedup_merges_phone_from_duplicate_entries()
    {
        var entries = new List<(ExtractedClient, DocumentReference?)>
        {
            (MakeClient("Amy Clark", "amy@example.com", phone: null), null),
            (MakeClient("Amy Clark", "amy@example.com", phone: "555-999-0001"), null),
        };

        var result = ContactClassifier.ClassifyAndDedup(entries);

        result.Should().HaveCount(1);
        result[0].Phone.Should().Be("555-999-0001");
    }
}
