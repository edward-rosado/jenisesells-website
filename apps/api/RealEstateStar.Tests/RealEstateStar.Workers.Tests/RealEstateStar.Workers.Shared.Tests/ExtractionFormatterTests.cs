using FluentAssertions;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Workers.Shared.Tests;

public class ExtractionFormatterTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static DocumentExtraction MakeExtraction(
        DocumentType type = DocumentType.ListingAgreement,
        string? transactionStatus = "Active",
        ExtractedProperty? property = null,
        ExtractedKeyTerms? keyTerms = null,
        IReadOnlyList<ExtractedClient>? clients = null,
        IReadOnlyList<string>? serviceAreas = null,
        DateTime? date = null)
        => new(
            DriveFileId: "file-1",
            FileName: "test.pdf",
            Type: type,
            Clients: clients ?? new List<ExtractedClient>(),
            Property: property,
            Date: date,
            KeyTerms: keyTerms,
            TransactionStatus: transactionStatus,
            ServiceAreas: serviceAreas);

    private static ExtractedKeyTerms MakeKeyTerms(string? price = null, string? commission = null)
        => new(Price: price, Commission: commission, Contingencies: new List<string>());

    private static ExtractedProperty MakeProperty(
        string address = "123 Main St",
        string? city = "Springfield",
        string? state = "NJ",
        string? zip = "07001")
        => new(Address: address, City: city, State: state, Zip: zip);

    // ---------------------------------------------------------------------------
    // FormatExtractions — empty list
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatExtractions_EmptyList_ReturnsNoDataMessage()
    {
        var result = ExtractionFormatter.FormatExtractions(new List<DocumentExtraction>());

        result.Should().Be("(No pre-extracted transaction data available)");
    }

    // ---------------------------------------------------------------------------
    // FormatExtractions — single extraction with all fields populated
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatExtractions_SingleExtractionAllFields_FormatsAllFields()
    {
        var date = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        var extraction = MakeExtraction(
            type: DocumentType.ListingAgreement,
            transactionStatus: "Active",
            property: MakeProperty("456 Oak Ave", "Trenton", "NJ", "08601"),
            keyTerms: MakeKeyTerms(price: "$550,000", commission: "2.5%"),
            clients: new List<ExtractedClient>
            {
                new("Jane Smith", ContactRole.Seller, "jane@example.com", null),
                new("John Buyer", ContactRole.Buyer, null, "555-1234")
            },
            serviceAreas: new List<string> { "Trenton", "Princeton" },
            date: date);

        var result = ExtractionFormatter.FormatExtractions(new List<DocumentExtraction> { extraction });

        result.Should().Contain("Type: ListingAgreement");
        result.Should().Contain("Status: Active");
        result.Should().Contain("Property: 456 Oak Ave, Trenton, NJ, 08601");
        result.Should().Contain("Price: $550,000");
        result.Should().Contain("Commission: 2.5%");
        result.Should().Contain("Clients: Jane Smith (Seller), John Buyer (Buyer)");
        result.Should().Contain("Service Areas: Trenton, Princeton");
        result.Should().Contain("Date: 2024-06-15");
    }

    // ---------------------------------------------------------------------------
    // FormatExtractions — null optional fields
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatExtractions_NullOptionalFields_OmitsNullSections()
    {
        var extraction = MakeExtraction(
            type: DocumentType.Other,
            transactionStatus: null,
            property: null,
            keyTerms: null,
            serviceAreas: null,
            date: null);

        var result = ExtractionFormatter.FormatExtractions(new List<DocumentExtraction> { extraction });

        result.Should().Contain("Type: Other");
        result.Should().Contain("Status: Unknown");
        result.Should().NotContain("Property:");
        result.Should().NotContain("Price:");
        result.Should().NotContain("Commission:");
        result.Should().NotContain("Service Areas:");
        result.Should().NotContain("Date:");
    }

    // ---------------------------------------------------------------------------
    // FormatExtractions — maxCount limits output
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatExtractions_MaxCountLimitsOutput()
    {
        var extractions = new List<DocumentExtraction>
        {
            MakeExtraction(type: DocumentType.ListingAgreement, transactionStatus: "Active"),
            MakeExtraction(type: DocumentType.PurchaseContract, transactionStatus: "Pending")
        };

        var result = ExtractionFormatter.FormatExtractions(extractions, maxCount: 1);

        result.Should().Contain("1 of 2");
        result.Should().Contain("ListingAgreement");
        result.Should().NotContain("PurchaseContract");
    }

    // ---------------------------------------------------------------------------
    // FormatFeeExtractions — filters to commission-bearing extractions only
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatFeeExtractions_FiltersToCommissionBearingOnly()
    {
        var withCommission = MakeExtraction(
            type: DocumentType.ListingAgreement,
            transactionStatus: "Active",
            keyTerms: MakeKeyTerms(price: "$400,000", commission: "3%"));

        var withoutCommission = MakeExtraction(
            type: DocumentType.PurchaseContract,
            transactionStatus: "Pending",
            keyTerms: MakeKeyTerms(price: "$300,000", commission: null));

        var noKeyTerms = MakeExtraction(
            type: DocumentType.Disclosure,
            keyTerms: null);

        var extractions = new List<DocumentExtraction> { withCommission, withoutCommission, noKeyTerms };

        var result = ExtractionFormatter.FormatFeeExtractions(extractions);

        result.Should().Contain("Commission: 3%");
        result.Should().NotContain("PurchaseContract");
        result.Should().NotContain("Disclosure");
    }

    // ---------------------------------------------------------------------------
    // FormatFeeExtractions — no commission-bearing extractions
    // ---------------------------------------------------------------------------

    [Fact]
    public void FormatFeeExtractions_NoCommissionBearingExtractions_ReturnsNoCommissionMessage()
    {
        var extractions = new List<DocumentExtraction>
        {
            MakeExtraction(keyTerms: MakeKeyTerms(price: "$300,000", commission: null)),
            MakeExtraction(keyTerms: null)
        };

        var result = ExtractionFormatter.FormatFeeExtractions(extractions);

        result.Should().Be("(No pre-extracted commission data available)");
    }

    [Fact]
    public void FormatFeeExtractions_EmptyList_ReturnsNoCommissionMessage()
    {
        var result = ExtractionFormatter.FormatFeeExtractions(new List<DocumentExtraction>());

        result.Should().Be("(No pre-extracted commission data available)");
    }
}
