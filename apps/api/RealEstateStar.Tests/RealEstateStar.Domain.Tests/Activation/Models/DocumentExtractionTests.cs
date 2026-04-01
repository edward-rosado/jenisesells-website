using System.Text.Json;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Tests.Activation.Models;

public class DocumentExtractionTests
{
    [Fact]
    public void DocumentExtraction_roundtrips_through_json()
    {
        var extraction = new DocumentExtraction(
            DriveFileId: "file123", FileName: "Purchase Agreement.pdf",
            Type: DocumentType.PurchaseContract,
            Clients: new[] {
                new ExtractedClient("Jane Doe", ContactRole.Buyer, "jane@example.com", "555-0100"),
                new ExtractedClient("John Smith", ContactRole.Seller, null, null)
            },
            Property: new ExtractedProperty("123 Main St", "Springfield", "NJ", "07081"),
            Date: new DateTime(2026, 1, 15),
            KeyTerms: new ExtractedKeyTerms("$450,000", "6%", new[] { "Inspection", "Financing" }));

        var json = JsonSerializer.Serialize(extraction);
        var deserialized = JsonSerializer.Deserialize<DocumentExtraction>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("file123", deserialized.DriveFileId);
        Assert.Equal(DocumentType.PurchaseContract, deserialized.Type);
        Assert.Equal(2, deserialized.Clients.Count);
        Assert.Equal("Jane Doe", deserialized.Clients[0].Name);
        Assert.Equal(ContactRole.Buyer, deserialized.Clients[0].Role);
        Assert.Equal("123 Main St", deserialized.Property!.Address);
        Assert.Equal("$450,000", deserialized.KeyTerms!.Price);
        Assert.Equal(2, deserialized.KeyTerms.Contingencies.Count);
    }
}

public class ImportedContactTests
{
    [Fact]
    public void ImportedContact_roundtrips_through_json()
    {
        var contact = new ImportedContact(
            Name: "Jane Doe", Email: "jane@example.com", Phone: "555-0100",
            Role: ContactRole.Buyer, Stage: PipelineStage.UnderContract,
            PropertyAddress: "123 Main St",
            Documents: new[] {
                new DocumentReference("file123", "Purchase Agreement.pdf",
                    DocumentType.PurchaseContract, new DateTime(2026, 1, 15))
            });

        var json = JsonSerializer.Serialize(contact);
        var deserialized = JsonSerializer.Deserialize<ImportedContact>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("Jane Doe", deserialized.Name);
        Assert.Equal(PipelineStage.UnderContract, deserialized.Stage);
        Assert.Single(deserialized.Documents);
        Assert.Equal(DocumentType.PurchaseContract, deserialized.Documents[0].Type);
    }
}
