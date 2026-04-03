using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.DriveIndex.Tests;

/// <summary>
/// Tests for DriveIndexWorker contact extraction: ParseDocumentExtraction,
/// ParseDocumentType, ParseContactRole, and RunAsync with Claude mock producing extractions.
/// </summary>
public class DriveIndexExtractionTests
{
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";

    // ── ParseDocumentType ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("ListingAgreement", DocumentType.ListingAgreement)]
    [InlineData("BuyerAgreement", DocumentType.BuyerAgreement)]
    [InlineData("PurchaseContract", DocumentType.PurchaseContract)]
    [InlineData("Disclosure", DocumentType.Disclosure)]
    [InlineData("ClosingStatement", DocumentType.ClosingStatement)]
    [InlineData("Cma", DocumentType.Cma)]
    [InlineData("Inspection", DocumentType.Inspection)]
    [InlineData("Appraisal", DocumentType.Appraisal)]
    [InlineData("Other", DocumentType.Other)]
    [InlineData("unknown-value", DocumentType.Other)]
    [InlineData(null, DocumentType.Other)]
    public void ParseDocumentType_MapsAllValues(string? input, DocumentType expected)
    {
        var result = DriveIndexWorker.ParseDocumentType(input);

        result.Should().Be(expected);
    }

    // ── ParseContactRole ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Buyer", ContactRole.Buyer)]
    [InlineData("Seller", ContactRole.Seller)]
    [InlineData("Both", ContactRole.Both)]
    [InlineData("Unknown", ContactRole.Unknown)]
    [InlineData("random", ContactRole.Unknown)]
    [InlineData(null, ContactRole.Unknown)]
    public void ParseContactRole_MapsAllValues(string? input, ContactRole expected)
    {
        var result = DriveIndexWorker.ParseContactRole(input);

        result.Should().Be(expected);
    }

    // ── ParseDocumentExtraction: invalid JSON ─────────────────────────────────

    [Fact]
    public void ParseDocumentExtraction_NullJson_ReturnsNull()
    {
        var result = DriveIndexWorker.ParseDocumentExtraction("file-1", "test.pdf", null!);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseDocumentExtraction_EmptyJson_ReturnsNull()
    {
        var result = DriveIndexWorker.ParseDocumentExtraction("file-1", "test.pdf", "");

        result.Should().BeNull();
    }

    [Fact]
    public void ParseDocumentExtraction_WhitespaceJson_ReturnsNull()
    {
        var result = DriveIndexWorker.ParseDocumentExtraction("file-1", "test.pdf", "   ");

        result.Should().BeNull();
    }

    [Fact]
    public void ParseDocumentExtraction_InvalidJson_ReturnsNull()
    {
        var result = DriveIndexWorker.ParseDocumentExtraction("file-1", "test.pdf", "not json {{{");

        result.Should().BeNull();
    }

    [Fact]
    public void ParseDocumentExtraction_EmptyObject_ReturnsExtractionWithDefaults()
    {
        var result = DriveIndexWorker.ParseDocumentExtraction("file-1", "test.pdf", "{}");

        result.Should().NotBeNull();
        result!.DriveFileId.Should().Be("file-1");
        result.FileName.Should().Be("test.pdf");
        result.Type.Should().Be(DocumentType.Other);
        result.Clients.Should().BeEmpty();
        result.Property.Should().BeNull();
        result.Date.Should().BeNull();
        result.KeyTerms.Should().BeNull();
    }

    // ── ParseDocumentExtraction: valid full JSON ──────────────────────────────

    [Fact]
    public void ParseDocumentExtraction_FullValidJson_ExtractsAllFields()
    {
        const string json = """
            {
                "type": "PurchaseContract",
                "date": "2024-03-15",
                "clients": [
                    {
                        "name": "John Doe",
                        "role": "Buyer",
                        "email": "john@example.com",
                        "phone": "555-1234"
                    },
                    {
                        "name": "Jane Smith",
                        "role": "Seller",
                        "email": null,
                        "phone": null
                    }
                ],
                "property": {
                    "address": "123 Main St",
                    "city": "Springfield",
                    "state": "NJ",
                    "zip": "07001"
                },
                "keyTerms": {
                    "price": "$450,000",
                    "commission": "3%",
                    "contingencies": ["Financing", "Inspection"]
                }
            }
            """;

        var result = DriveIndexWorker.ParseDocumentExtraction("doc-1", "Purchase Contract.pdf", json);

        result.Should().NotBeNull();
        result!.DriveFileId.Should().Be("doc-1");
        result.FileName.Should().Be("Purchase Contract.pdf");
        result.Type.Should().Be(DocumentType.PurchaseContract);
        result.Date.Should().Be(new DateTime(2024, 3, 15));

        result.Clients.Should().HaveCount(2);
        result.Clients[0].Name.Should().Be("John Doe");
        result.Clients[0].Role.Should().Be(ContactRole.Buyer);
        result.Clients[0].Email.Should().Be("john@example.com");
        result.Clients[0].Phone.Should().Be("555-1234");
        result.Clients[1].Name.Should().Be("Jane Smith");
        result.Clients[1].Role.Should().Be(ContactRole.Seller);
        result.Clients[1].Email.Should().BeNull();

        result.Property.Should().NotBeNull();
        result.Property!.Address.Should().Be("123 Main St");
        result.Property.City.Should().Be("Springfield");
        result.Property.State.Should().Be("NJ");
        result.Property.Zip.Should().Be("07001");

        result.KeyTerms.Should().NotBeNull();
        result.KeyTerms!.Price.Should().Be("$450,000");
        result.KeyTerms.Commission.Should().Be("3%");
        result.KeyTerms.Contingencies.Should().BeEquivalentTo(["Financing", "Inspection"]);
    }

    [Fact]
    public void ParseDocumentExtraction_ClientWithNoName_IsSkipped()
    {
        const string json = """
            {
                "clients": [
                    { "name": "", "role": "Buyer" },
                    { "name": "Valid Person", "role": "Seller" }
                ]
            }
            """;

        var result = DriveIndexWorker.ParseDocumentExtraction("f", "doc.pdf", json);

        result!.Clients.Should().HaveCount(1);
        result.Clients[0].Name.Should().Be("Valid Person");
    }

    [Fact]
    public void ParseDocumentExtraction_PropertyWithNoAddress_IsNull()
    {
        const string json = """
            {
                "property": {
                    "address": "",
                    "city": "Springfield"
                }
            }
            """;

        var result = DriveIndexWorker.ParseDocumentExtraction("f", "doc.pdf", json);

        result!.Property.Should().BeNull();
    }

    [Fact]
    public void ParseDocumentExtraction_NullContingencies_UsesEmptyList()
    {
        const string json = """
            {
                "keyTerms": {
                    "price": "$300,000",
                    "commission": null,
                    "contingencies": []
                }
            }
            """;

        var result = DriveIndexWorker.ParseDocumentExtraction("f", "doc.pdf", json);

        result!.KeyTerms!.Contingencies.Should().BeEmpty();
        result.KeyTerms.Commission.Should().BeNull();
    }

    // ── RunAsync with Claude producing extractions ────────────────────────────

    [Fact]
    public async Task RunAsync_WithTextDocAndClaudeResponse_ReturnsExtraction()
    {
        const string extractionJson = """
            {
                "type": "ListingAgreement",
                "date": null,
                "clients": [{ "name": "Alice Buyer", "role": "Buyer", "email": null, "phone": null }],
                "property": { "address": "42 Oak Ave", "city": null, "state": null, "zip": null },
                "keyTerms": { "price": null, "commission": null, "contingencies": [] }
            }
            """;

        var files = new List<DriveFileInfo>
        {
            new("doc-1", "Listing Agreement.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DateTime.UtcNow)
        };

        var driveClient = new Mock<IGDriveClient>();
        driveClient.Setup(c => c.GetOrCreateFolderAsync(AccountId, AgentId, DriveIndexWorker.PlatformFolderName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-id");
        driveClient.Setup(c => c.ListAllFilesAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);
        driveClient.Setup(c => c.GetFileContentAsync(AccountId, AgentId, "doc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Listing agreement content for Alice Buyer at 42 Oak Ave.");
        driveClient.Setup(c => c.DownloadBinaryAsync(AccountId, AgentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var anthropic = new Mock<IAnthropicClient>();
        anthropic.Setup(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse(extractionJson, 50, 100, 200));

        var worker = new DriveIndexWorker(driveClient.Object, anthropic.Object, NullLogger<DriveIndexWorker>.Instance);

        var result = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        result.Extractions.Should().HaveCount(1);
        result.Extractions[0].Type.Should().Be(DocumentType.ListingAgreement);
        result.Extractions[0].Clients.Should().HaveCount(1);
        result.Extractions[0].Clients[0].Name.Should().Be("Alice Buyer");
    }

    [Fact]
    public async Task RunAsync_WithClaudeReturningInvalidJson_ReturnsNoExtractions()
    {
        var files = new List<DriveFileInfo>
        {
            new("doc-1", "Listing Agreement.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DateTime.UtcNow)
        };

        var driveClient = new Mock<IGDriveClient>();
        driveClient.Setup(c => c.GetOrCreateFolderAsync(AccountId, AgentId, DriveIndexWorker.PlatformFolderName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-id");
        driveClient.Setup(c => c.ListAllFilesAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);
        driveClient.Setup(c => c.GetFileContentAsync(AccountId, AgentId, "doc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Some listing content.");
        driveClient.Setup(c => c.DownloadBinaryAsync(AccountId, AgentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var anthropic = new Mock<IAnthropicClient>();
        anthropic.Setup(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnthropicResponse("not valid json at all", 10, 20, 50));

        var worker = new DriveIndexWorker(driveClient.Object, anthropic.Object, NullLogger<DriveIndexWorker>.Instance);

        var result = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        result.Extractions.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WhenClaudeThrows_ContinuesAndReturnsNoExtractions()
    {
        var files = new List<DriveFileInfo>
        {
            new("doc-1", "Listing Agreement.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                DateTime.UtcNow)
        };

        var driveClient = new Mock<IGDriveClient>();
        driveClient.Setup(c => c.GetOrCreateFolderAsync(AccountId, AgentId, DriveIndexWorker.PlatformFolderName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("folder-id");
        driveClient.Setup(c => c.ListAllFilesAsync(AccountId, AgentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(files);
        driveClient.Setup(c => c.GetFileContentAsync(AccountId, AgentId, "doc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Some listing content.");
        driveClient.Setup(c => c.DownloadBinaryAsync(AccountId, AgentId, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var anthropic = new Mock<IAnthropicClient>();
        anthropic.Setup(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Claude API unavailable"));

        var worker = new DriveIndexWorker(driveClient.Object, anthropic.Object, NullLogger<DriveIndexWorker>.Instance);

        var act = async () => await worker.RunAsync(AccountId, AgentId, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
