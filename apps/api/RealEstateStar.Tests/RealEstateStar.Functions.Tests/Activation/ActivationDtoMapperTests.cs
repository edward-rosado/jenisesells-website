using FluentAssertions;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Functions.Activation.Dtos;

namespace RealEstateStar.Functions.Tests.Activation;

/// <summary>
/// Roundtrip serialization tests for all ActivationDtoMapper conversions.
/// Ensures no data is lost when crossing the Durable activity boundary.
/// </summary>
public sealed class ActivationDtoMapperTests
{
    // ── EmailCorpus roundtrip ─────────────────────────────────────────────────

    [Fact]
    public void EmailCorpus_Roundtrip_PreservesAllFields()
    {
        var sig = new EmailSignature(
            Name: "Jane Smith",
            Title: "REALTOR",
            Phone: "555-1234",
            LicenseNumber: "NJ-123",
            BrokerageName: "ABC Realty",
            SocialLinks: ["https://linkedin.com/in/jane"],
            HeadshotUrl: "https://example.com/headshot.jpg",
            WebsiteUrl: "https://jane.com",
            LogoUrl: "https://example.com/logo.png");

        var msg = new EmailMessage(
            Id: "msg1",
            Subject: "Hello",
            Body: "Body text",
            From: "jane@example.com",
            To: ["client@example.com"],
            Date: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            SignatureBlock: "Regards, Jane");

        var corpus = new EmailCorpus(
            SentEmails: [msg],
            InboxEmails: [],
            Signature: sig);

        // Act
        var dto = ActivationDtoMapper.ToDto(corpus);
        var roundtripped = ActivationDtoMapper.ToDomain(dto);

        // Assert
        roundtripped.SentEmails.Should().HaveCount(1);
        roundtripped.SentEmails[0].Id.Should().Be(msg.Id);
        roundtripped.SentEmails[0].Subject.Should().Be(msg.Subject);
        roundtripped.SentEmails[0].Body.Should().Be(msg.Body);
        roundtripped.SentEmails[0].From.Should().Be(msg.From);
        roundtripped.SentEmails[0].To.Should().BeEquivalentTo(msg.To);
        roundtripped.SentEmails[0].Date.Should().Be(msg.Date);
        roundtripped.SentEmails[0].SignatureBlock.Should().Be(msg.SignatureBlock);

        roundtripped.Signature.Should().NotBeNull();
        roundtripped.Signature!.Name.Should().Be(sig.Name);
        roundtripped.Signature.Title.Should().Be(sig.Title);
        roundtripped.Signature.Phone.Should().Be(sig.Phone);
        roundtripped.Signature.LicenseNumber.Should().Be(sig.LicenseNumber);
        roundtripped.Signature.BrokerageName.Should().Be(sig.BrokerageName);
        roundtripped.Signature.SocialLinks.Should().BeEquivalentTo(sig.SocialLinks);
        roundtripped.Signature.HeadshotUrl.Should().Be(sig.HeadshotUrl);
        roundtripped.Signature.WebsiteUrl.Should().Be(sig.WebsiteUrl);
        roundtripped.Signature.LogoUrl.Should().Be(sig.LogoUrl);
    }

    [Fact]
    public void EmailCorpus_NullSignature_RoundtripPreservesNull()
    {
        var corpus = new EmailCorpus(SentEmails: [], InboxEmails: [], Signature: null);

        var dto = ActivationDtoMapper.ToDto(corpus);
        var result = ActivationDtoMapper.ToDomain(dto);

        result.Signature.Should().BeNull();
    }

    // ── DriveIndex roundtrip ──────────────────────────────────────────────────

    [Fact]
    public void DriveIndex_Roundtrip_PreservesAllFields()
    {
        var extraction = new DocumentExtraction(
            DriveFileId: "file1",
            FileName: "Contract.pdf",
            Type: DocumentType.PurchaseContract,
            Clients:
            [
                new ExtractedClient("Alice", ContactRole.Buyer, "alice@x.com", "555-0001"),
                new ExtractedClient("Bob", ContactRole.Seller, null, null),
            ],
            Property: new ExtractedProperty("123 Main St", "Anytown", "NJ", "07001"),
            Date: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            KeyTerms: new ExtractedKeyTerms("$500,000", "3%", ["inspection", "financing"]));

        var driveIndex = new DriveIndex(
            FolderId: "folder123",
            Files:
            [
                new DriveFile("f1", "Contract.pdf", "application/pdf", "Contract", new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            ],
            Contents: new Dictionary<string, string> { ["f1"] = "content" },
            DiscoveredUrls: ["https://zillow.com/profile"],
            Extractions: [extraction]);

        // Act
        var dto = ActivationDtoMapper.ToDto(driveIndex);
        var roundtripped = ActivationDtoMapper.ToDomain(dto);

        // Assert
        roundtripped.FolderId.Should().Be(driveIndex.FolderId);
        roundtripped.Files.Should().HaveCount(1);
        roundtripped.Files[0].Id.Should().Be("f1");
        roundtripped.Files[0].Category.Should().Be("Contract");
        roundtripped.DiscoveredUrls.Should().BeEquivalentTo(driveIndex.DiscoveredUrls);
        roundtripped.Extractions.Should().HaveCount(1);

        var ext = roundtripped.Extractions[0];
        ext.DriveFileId.Should().Be("file1");
        ext.FileName.Should().Be("Contract.pdf");
        ext.Type.Should().Be(DocumentType.PurchaseContract);
        ext.Clients.Should().HaveCount(2);
        ext.Clients[0].Name.Should().Be("Alice");
        ext.Clients[0].Role.Should().Be(ContactRole.Buyer);
        ext.Clients[0].Email.Should().Be("alice@x.com");
        ext.Clients[1].Name.Should().Be("Bob");
        ext.Clients[1].Role.Should().Be(ContactRole.Seller);
        ext.Property.Should().NotBeNull();
        ext.Property!.Address.Should().Be("123 Main St");
        ext.Property.City.Should().Be("Anytown");
        ext.Property.State.Should().Be("NJ");
        ext.KeyTerms.Should().NotBeNull();
        ext.KeyTerms!.Price.Should().Be("$500,000");
        ext.KeyTerms.Commission.Should().Be("3%");
        ext.KeyTerms.Contingencies.Should().BeEquivalentTo(["inspection", "financing"]);
    }

    [Fact]
    public void DocumentExtraction_NullOptionalFields_RoundtripPreservesNull()
    {
        var extraction = new DocumentExtraction(
            DriveFileId: "f1",
            FileName: "Doc.pdf",
            Type: DocumentType.Other,
            Clients: [],
            Property: null,
            Date: null,
            KeyTerms: null);

        var driveIndex = new DriveIndex("folder", [
            new DriveFile("f1", "Doc.pdf", "application/pdf", "Other", DateTime.UtcNow)
        ], new Dictionary<string, string>(), [], [extraction]);

        var dto = ActivationDtoMapper.ToDto(driveIndex);
        var result = ActivationDtoMapper.ToDomain(dto);

        result.Extractions[0].Property.Should().BeNull();
        result.Extractions[0].Date.Should().BeNull();
        result.Extractions[0].KeyTerms.Should().BeNull();
    }

    // ── AgentDiscovery roundtrip ──────────────────────────────────────────────

    [Fact]
    public void AgentDiscovery_Roundtrip_PreservesAllFields()
    {
        var profile = new ThirdPartyProfile(
            Platform: "Zillow",
            Bio: "Top producer",
            Reviews: [new Review("Great agent!", 5, "Client A", "Zillow", new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc))],
            SalesCount: 42,
            ActiveListingCount: 5,
            YearsExperience: 10,
            Specialties: ["luxury"],
            ServiceAreas: ["Montclair", "Livingston"],
            RecentSales: [new ListingInfo("456 Oak Ave", "Montclair", "NJ", "$750,000", "Sold", 4, 2, 2000, null, null)],
            ActiveListings: []);

        var discovery = new AgentDiscovery(
            HeadshotBytes: [1, 2, 3],
            LogoBytes: [4, 5, 6],
            Phone: "555-9999",
            Websites: [new DiscoveredWebsite("https://agent.com", "Google", "<html/>")],
            Reviews: [],
            Profiles: [profile],
            Ga4MeasurementId: "G-12345",
            WhatsAppEnabled: true);

        // Act
        var dto = ActivationDtoMapper.ToDto(discovery);
        var roundtripped = ActivationDtoMapper.ToDomain(dto);

        // Assert
        roundtripped.HeadshotBytes.Should().BeEquivalentTo(discovery.HeadshotBytes);
        roundtripped.LogoBytes.Should().BeEquivalentTo(discovery.LogoBytes);
        roundtripped.Phone.Should().Be(discovery.Phone);
        roundtripped.Ga4MeasurementId.Should().Be(discovery.Ga4MeasurementId);
        roundtripped.WhatsAppEnabled.Should().BeTrue();
        roundtripped.Websites.Should().HaveCount(1);
        roundtripped.Websites[0].Url.Should().Be("https://agent.com");
        roundtripped.Profiles.Should().HaveCount(1);
        roundtripped.Profiles[0].Platform.Should().Be("Zillow");
        roundtripped.Profiles[0].SalesCount.Should().Be(42);
        roundtripped.Profiles[0].ServiceAreas.Should().BeEquivalentTo(["Montclair", "Livingston"]);
        roundtripped.Profiles[0].Reviews.Should().HaveCount(1);
        roundtripped.Profiles[0].Reviews[0].Text.Should().Be("Great agent!");
        roundtripped.Profiles[0].RecentSales.Should().HaveCount(1);
        roundtripped.Profiles[0].RecentSales[0].Address.Should().Be("456 Oak Ave");
    }

    // ── ImportedContact roundtrip ─────────────────────────────────────────────

    [Fact]
    public void ImportedContact_Roundtrip_PreservesAllFields()
    {
        var contact = new ImportedContact(
            Name: "Alice Smith",
            Email: "alice@x.com",
            Phone: "555-1111",
            Role: ContactRole.Buyer,
            Stage: PipelineStage.ActiveClient,
            PropertyAddress: "789 Elm St",
            Documents:
            [
                new DocumentReference("file1", "Contract.pdf", DocumentType.PurchaseContract,
                    new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            ]);

        // Act
        var dto = ActivationDtoMapper.ToDto(contact);
        var roundtripped = ActivationDtoMapper.ToDomain(dto);

        // Assert
        roundtripped.Name.Should().Be(contact.Name);
        roundtripped.Email.Should().Be(contact.Email);
        roundtripped.Phone.Should().Be(contact.Phone);
        roundtripped.Role.Should().Be(ContactRole.Buyer);
        roundtripped.Stage.Should().Be(PipelineStage.ActiveClient);
        roundtripped.PropertyAddress.Should().Be(contact.PropertyAddress);
        roundtripped.Documents.Should().HaveCount(1);
        roundtripped.Documents[0].DriveFileId.Should().Be("file1");
        roundtripped.Documents[0].FileName.Should().Be("Contract.pdf");
        roundtripped.Documents[0].Type.Should().Be(DocumentType.PurchaseContract);
    }

    // ── BrandingKit roundtrip ─────────────────────────────────────────────────

    [Fact]
    public void BrandingKitDto_ToDomain_PreservesAllFields()
    {
        var dto = new BrandingKitDto(
            Colors: [new ColorEntryDto("primary", "#FF0000", "website", "buttons")],
            Fonts: [new FontEntryDto("heading", "Roboto", "700", "google")],
            Logos: [new LogoVariantDto("light", "logo-light.png", [9, 8, 7], "website")],
            RecommendedTemplate: "modern",
            TemplateReason: "Matches branding");

        var domain = ActivationDtoMapper.ToDomain(dto);

        domain.Should().NotBeNull();
        domain!.Colors.Should().HaveCount(1);
        domain.Colors[0].Role.Should().Be("primary");
        domain.Colors[0].Hex.Should().Be("#FF0000");
        domain.Fonts.Should().HaveCount(1);
        domain.Fonts[0].Family.Should().Be("Roboto");
        domain.Logos.Should().HaveCount(1);
        domain.Logos[0].Bytes.Should().BeEquivalentTo(new byte[] { 9, 8, 7 });
        domain.RecommendedTemplate.Should().Be("modern");
        domain.TemplateReason.Should().Be("Matches branding");
    }

    [Fact]
    public void BrandingKitDto_Null_ReturnsDomainNull()
    {
        var result = ActivationDtoMapper.ToDomain((BrandingKitDto?)null);
        result.Should().BeNull();
    }

    // ── BuildActivationOutputs ────────────────────────────────────────────────

    [Fact]
    public void BuildActivationOutputs_MapsAllFields()
    {
        var discoveryOutput = new AgentDiscoveryOutput(
            HeadshotBytes: [1, 2],
            LogoBytes: [3, 4],
            Phone: "555-0001",
            Websites: [],
            Reviews: [],
            Profiles: [],
            Ga4MeasurementId: null,
            WhatsAppEnabled: false);

        var input = new PersistProfileInput(
            AccountId: "acc1",
            AgentId: "agent1",
            Handle: "agent1",
            Voice: new VoiceExtractionOutput("Voice content", false),
            Personality: new PersonalityOutput("Personality content", false),
            CmaStyle: "CMA style",
            Marketing: new MarketingStyleOutput("Marketing guide", "Brand signals"),
            WebsiteStyle: "Website style",
            SalesPipeline: "Pipeline",
            Coaching: new CoachingOutput("Coaching report", false),
            Branding: new BrandingDiscoveryOutput("Branding kit", null),
            BrandExtraction: "Brand signals",
            BrandVoice: "Voice signals",
            Compliance: "Compliance report",
            FeeStructure: "Fee structure",
            DriveIndexMarkdown: "# Drive Index",
            DiscoveryMarkdown: "# Discovery",
            EmailSignatureMarkdown: "# Signature",
            HeadshotBytes: [1, 2],
            BrokerageLogoBytes: [3, 4],
            AgentName: "Jane Smith",
            AgentEmail: "jane@example.com",
            AgentPhone: "555-1234",
            AgentTitle: "REALTOR",
            AgentLicenseNumber: "NJ-123",
            ServiceAreas: ["Montclair"],
            Discovery: discoveryOutput);

        var outputs = ActivationDtoMapper.BuildActivationOutputs(input);

        outputs.VoiceSkill.Should().Be("Voice content");
        outputs.PersonalitySkill.Should().Be("Personality content");
        outputs.CmaStyleGuide.Should().Be("CMA style");
        outputs.MarketingStyle.Should().Be("Marketing guide");
        outputs.WebsiteStyleGuide.Should().Be("Website style");
        outputs.SalesPipeline.Should().Be("Pipeline");
        outputs.CoachingReport.Should().Be("Coaching report");
        outputs.BrandingKitMarkdown.Should().Be("Branding kit");
        outputs.BrandExtractionSignals.Should().Be("Brand signals");
        outputs.BrandVoiceSignals.Should().Be("Voice signals");
        outputs.ComplianceAnalysis.Should().Be("Compliance report");
        outputs.FeeStructure.Should().Be("Fee structure");
        outputs.DriveIndex.Should().Be("# Drive Index");
        outputs.AgentDiscoveryMarkdown.Should().Be("# Discovery");
        outputs.EmailSignature.Should().Be("# Signature");
        outputs.HeadshotBytes.Should().BeEquivalentTo(new byte[] { 1, 2 });
        outputs.BrokerageLogoBytes.Should().BeEquivalentTo(new byte[] { 3, 4 });
        outputs.AgentName.Should().Be("Jane Smith");
        outputs.AgentEmail.Should().Be("jane@example.com");
        outputs.AgentPhone.Should().Be("555-1234");
        outputs.AgentTitle.Should().Be("REALTOR");
        outputs.AgentLicenseNumber.Should().Be("NJ-123");
        outputs.ServiceAreas.Should().BeEquivalentTo(["Montclair"]);
        outputs.Discovery.Should().NotBeNull();
        outputs.Discovery!.Phone.Should().Be("555-0001");
    }

    [Fact]
    public void BuildActivationOutputs_NullWorkerOutputs_MapsToNulls()
    {
        var discoveryOutput = new AgentDiscoveryOutput(
            HeadshotBytes: null,
            LogoBytes: null,
            Phone: null,
            Websites: [],
            Reviews: [],
            Profiles: [],
            Ga4MeasurementId: null,
            WhatsAppEnabled: false);

        var input = new PersistProfileInput(
            AccountId: "acc1",
            AgentId: "agent1",
            Handle: "agent1",
            Voice: null,
            Personality: null,
            CmaStyle: null,
            Marketing: null,
            WebsiteStyle: null,
            SalesPipeline: null,
            Coaching: null,
            Branding: null,
            BrandExtraction: null,
            BrandVoice: null,
            Compliance: null,
            FeeStructure: null,
            DriveIndexMarkdown: "# Drive Index",
            DiscoveryMarkdown: "# Discovery",
            EmailSignatureMarkdown: null,
            HeadshotBytes: null,
            BrokerageLogoBytes: null,
            AgentName: null,
            AgentEmail: "jane@example.com",
            AgentPhone: null,
            AgentTitle: null,
            AgentLicenseNumber: null,
            ServiceAreas: [],
            Discovery: discoveryOutput);

        var outputs = ActivationDtoMapper.BuildActivationOutputs(input);

        outputs.VoiceSkill.Should().BeNull();
        outputs.PersonalitySkill.Should().BeNull();
        outputs.CmaStyleGuide.Should().BeNull();
        outputs.MarketingStyle.Should().BeNull();
        outputs.CoachingReport.Should().BeNull();
        outputs.BrandingKitMarkdown.Should().BeNull();
        outputs.BrandingKit.Should().BeNull();
        outputs.HeadshotBytes.Should().BeNull();
        outputs.BrokerageLogoBytes.Should().BeNull();
        outputs.AgentName.Should().BeNull();
    }
}
