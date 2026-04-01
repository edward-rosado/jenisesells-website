using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.Activities.Activation.ContactImportPersist.Tests;

public class ContactImportPersistActivityTests
{
    private readonly Mock<IDocumentStorageProvider> _storage = new(MockBehavior.Strict);
    private readonly Mock<IGDriveClient> _driveClient = new(MockBehavior.Strict);
    private readonly Mock<ILeadStore> _leadStore = new(MockBehavior.Strict);
    private readonly ContactImportPersistActivity _sut;

    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private static readonly CancellationToken Ct = CancellationToken.None;

    public ContactImportPersistActivityTests()
    {
        _sut = new ContactImportPersistActivity(
            _storage.Object,
            _driveClient.Object,
            _leadStore.Object,
            NullLogger<ContactImportPersistActivity>.Instance);
    }

    // ── Static helpers: GetStageFolderName ────────────────────────────────────

    [Theory]
    [InlineData(PipelineStage.Lead, "1 - Leads")]
    [InlineData(PipelineStage.ActiveClient, "2 - Active Clients")]
    [InlineData(PipelineStage.UnderContract, "3 - Under Contract")]
    [InlineData(PipelineStage.Closed, "4 - Closed")]
    public void GetStageFolderName_ReturnsCorrectFolder(PipelineStage stage, string expected)
    {
        var result = ContactImportPersistActivity.GetStageFolderName(stage);

        result.Should().Be(expected);
    }

    [Fact]
    public void GetStageFolderName_UnknownStage_ReturnsInactiveFolder()
    {
        var result = ContactImportPersistActivity.GetStageFolderName((PipelineStage)99);

        result.Should().Be("5 - Inactive");
    }

    // ── Static helpers: GetStageSubFolders ────────────────────────────────────

    [Fact]
    public void GetStageSubFolders_Lead_WithoutAddress_ReturnsCommunicationsOnly()
    {
        var result = ContactImportPersistActivity.GetStageSubFolders(PipelineStage.Lead, null);

        result.Should().ContainSingle().Which.Should().Be("Communications");
    }

    [Fact]
    public void GetStageSubFolders_Lead_WithAddress_IncludesAddressSubFolder()
    {
        var result = ContactImportPersistActivity.GetStageSubFolders(PipelineStage.Lead, "123 Main St");

        result.Should().HaveCount(2);
        result.Should().Contain("Communications");
        result.Should().Contain("123 Main St");
    }

    [Fact]
    public void GetStageSubFolders_ActiveClient_ReturnsExpectedFolders()
    {
        var result = ContactImportPersistActivity.GetStageSubFolders(PipelineStage.ActiveClient, null);

        result.Should().BeEquivalentTo(["Agreements", "Documents Sent", "Communications"]);
    }

    [Fact]
    public void GetStageSubFolders_UnderContract_WithoutAddress_UsesContractsFolder()
    {
        var result = ContactImportPersistActivity.GetStageSubFolders(PipelineStage.UnderContract, null);

        result.Should().Contain("Contracts");
        result.Should().Contain("Inspection");
        result.Should().Contain("Appraisal");
        result.Should().Contain("Communications");
    }

    [Fact]
    public void GetStageSubFolders_UnderContract_WithAddress_UsesAddressTransactionFolder()
    {
        var result = ContactImportPersistActivity.GetStageSubFolders(PipelineStage.UnderContract, "123 Main St");

        result.Should().Contain("123 Main St Transaction/Contracts");
        result.Should().Contain("Inspection");
        result.Should().Contain("Appraisal");
        result.Should().Contain("Communications");
    }

    [Fact]
    public void GetStageSubFolders_Closed_ReturnsExpectedFolders()
    {
        var result = ContactImportPersistActivity.GetStageSubFolders(PipelineStage.Closed, null);

        result.Should().BeEquivalentTo(["Audit Log", "Reports", "Communications"]);
    }

    [Fact]
    public void GetStageSubFolders_UnknownStage_ReturnsEmptyList()
    {
        var result = ContactImportPersistActivity.GetStageSubFolders((PipelineStage)99, null);

        result.Should().BeEmpty();
    }

    // ── Static helpers: GetDocumentSubFolder ──────────────────────────────────

    [Theory]
    [InlineData(PipelineStage.ActiveClient, DocumentType.ListingAgreement, null, "Agreements")]
    [InlineData(PipelineStage.ActiveClient, DocumentType.BuyerAgreement, null, "Agreements")]
    [InlineData(PipelineStage.UnderContract, DocumentType.Inspection, null, "Inspection")]
    [InlineData(PipelineStage.UnderContract, DocumentType.Appraisal, null, "Appraisal")]
    [InlineData(PipelineStage.Closed, DocumentType.ClosingStatement, null, "Audit Log")]
    [InlineData(PipelineStage.Closed, DocumentType.Cma, null, "Reports")]
    public void GetDocumentSubFolder_ReturnsCorrectSubFolder(
        PipelineStage stage,
        DocumentType docType,
        string? address,
        string expected)
    {
        var result = ContactImportPersistActivity.GetDocumentSubFolder(stage, docType, address);

        result.Should().Be(expected);
    }

    [Fact]
    public void GetDocumentSubFolder_UnderContract_PurchaseContract_WithoutAddress_ReturnsContracts()
    {
        var result = ContactImportPersistActivity.GetDocumentSubFolder(
            PipelineStage.UnderContract, DocumentType.PurchaseContract, null);

        result.Should().Be("Contracts");
    }

    [Fact]
    public void GetDocumentSubFolder_UnderContract_PurchaseContract_WithAddress_ReturnsAddressContracts()
    {
        var result = ContactImportPersistActivity.GetDocumentSubFolder(
            PipelineStage.UnderContract, DocumentType.PurchaseContract, "123 Main St");

        result.Should().Be("123 Main St Transaction/Contracts");
    }

    [Fact]
    public void GetDocumentSubFolder_UnmappedDocumentType_ReturnsNull()
    {
        var result = ContactImportPersistActivity.GetDocumentSubFolder(
            PipelineStage.Lead, DocumentType.Other, null);

        result.Should().BeNull();
    }

    [Fact]
    public void GetDocumentSubFolder_ActiveClient_Disclosure_ReturnsNull()
    {
        var result = ContactImportPersistActivity.GetDocumentSubFolder(
            PipelineStage.ActiveClient, DocumentType.Disclosure, null);

        result.Should().BeNull();
    }

    // ── Static helpers: MapStageToLeadStatus ──────────────────────────────────

    [Theory]
    [InlineData(PipelineStage.Lead, LeadStatus.Received)]
    [InlineData(PipelineStage.ActiveClient, LeadStatus.ActiveClient)]
    [InlineData(PipelineStage.UnderContract, LeadStatus.UnderContract)]
    [InlineData(PipelineStage.Closed, LeadStatus.Closed)]
    public void MapStageToLeadStatus_ReturnsCorrectStatus(PipelineStage stage, LeadStatus expected)
    {
        var result = ContactImportPersistActivity.MapStageToLeadStatus(stage);

        result.Should().Be(expected);
    }

    [Fact]
    public void MapStageToLeadStatus_UnknownStage_ReturnsInactive()
    {
        var result = ContactImportPersistActivity.MapStageToLeadStatus((PipelineStage)99);

        result.Should().Be(LeadStatus.Inactive);
    }

    // ── Static helpers: MapRoleToLeadType ─────────────────────────────────────

    [Theory]
    [InlineData(ContactRole.Buyer, LeadType.Buyer)]
    [InlineData(ContactRole.Seller, LeadType.Seller)]
    [InlineData(ContactRole.Both, LeadType.Both)]
    [InlineData(ContactRole.Unknown, LeadType.Buyer)]
    public void MapRoleToLeadType_ReturnsCorrectType(ContactRole role, LeadType expected)
    {
        var result = ContactImportPersistActivity.MapRoleToLeadType(role);

        result.Should().Be(expected);
    }

    // ── Static helpers: BuildImportSummaryMarkdown ────────────────────────────

    [Fact]
    public void BuildImportSummaryMarkdown_EmptyList_ProducesHeaderOnly()
    {
        var result = ContactImportPersistActivity.BuildImportSummaryMarkdown([]);

        result.Should().Contain("# Client Import Summary");
        result.Should().Contain("0 contact(s)");
        result.Should().Contain("| Name | Email | Phone | Role | Stage | Property | Documents |");
    }

    [Fact]
    public void BuildImportSummaryMarkdown_SingleContact_IncludesAllFields()
    {
        var contact = new ImportedContact(
            Name: "Jane Smith",
            Email: "jane@example.com",
            Phone: "555-1234",
            Role: ContactRole.Buyer,
            Stage: PipelineStage.Lead,
            PropertyAddress: "123 Oak Ave",
            Documents: []);

        var result = ContactImportPersistActivity.BuildImportSummaryMarkdown([contact]);

        result.Should().Contain("Jane Smith");
        result.Should().Contain("jane@example.com");
        result.Should().Contain("555-1234");
        result.Should().Contain("Buyer");
        result.Should().Contain("Lead");
        result.Should().Contain("123 Oak Ave");
        result.Should().Contain("| 0 |");
    }

    [Fact]
    public void BuildImportSummaryMarkdown_NullEmailAndPhone_UsesDash()
    {
        var contact = new ImportedContact(
            Name: "John Doe",
            Email: null,
            Phone: null,
            Role: ContactRole.Seller,
            Stage: PipelineStage.Closed,
            PropertyAddress: null,
            Documents: []);

        var result = ContactImportPersistActivity.BuildImportSummaryMarkdown([contact]);

        // The null fields should display as "—"
        result.Should().Contain("—");
    }

    [Fact]
    public void BuildImportSummaryMarkdown_DocumentCount_IsCorrect()
    {
        var docs = new List<DocumentReference>
        {
            new("file-id-1", "Agreement.pdf", DocumentType.ListingAgreement, null),
            new("file-id-2", "Contract.pdf", DocumentType.PurchaseContract, null),
            new("file-id-3", "Closing.pdf", DocumentType.ClosingStatement, null),
        };

        var contact = new ImportedContact(
            Name: "Alice Brown",
            Email: "alice@example.com",
            Phone: null,
            Role: ContactRole.Seller,
            Stage: PipelineStage.Closed,
            PropertyAddress: "456 Elm St",
            Documents: docs);

        var result = ContactImportPersistActivity.BuildImportSummaryMarkdown([contact]);

        result.Should().Contain("| 3 |");
    }

    [Fact]
    public void BuildImportSummaryMarkdown_PipeInName_IsEscaped()
    {
        var contact = new ImportedContact(
            Name: "Smith | Jones",
            Email: null,
            Phone: null,
            Role: ContactRole.Unknown,
            Stage: PipelineStage.Lead,
            PropertyAddress: null,
            Documents: []);

        var result = ContactImportPersistActivity.BuildImportSummaryMarkdown([contact]);

        result.Should().Contain("Smith \\| Jones");
    }

    [Fact]
    public void BuildImportSummaryMarkdown_MultipleContacts_AllIncluded()
    {
        var contacts = new List<ImportedContact>
        {
            new("Alice", "alice@test.com", null, ContactRole.Buyer, PipelineStage.Lead, null, []),
            new("Bob", "bob@test.com", null, ContactRole.Seller, PipelineStage.Closed, "789 Pine Rd", []),
        };

        var result = ContactImportPersistActivity.BuildImportSummaryMarkdown(contacts);

        result.Should().Contain("2 contact(s)");
        result.Should().Contain("Alice");
        result.Should().Contain("Bob");
    }

    // ── ExecuteAsync: top-level folder creation ────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CreatesAllTopLevelFolders()
    {
        var contacts = new List<ImportedContact>();

        SetupDriveGetOrCreateFolder(Times.Exactly(5));
        SetupStorageWriteImportSummary();

        await _sut.ExecuteAsync(AccountId, AgentId, contacts, Ct);

        foreach (var folder in ContactImportPersistActivity.TopLevelFolders)
        {
            _driveClient.Verify(d => d.GetOrCreateFolderAsync(AccountId, AgentId, folder, Ct), Times.Once);
        }
    }

    // ── ExecuteAsync: contact processing ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SingleContact_CreatesContactFolder()
    {
        var contact = MakeContact("Jane Doe", "jane@test.com", stage: PipelineStage.Lead);

        SetupDriveForContact(contact);
        SetupLeadStoreGetByEmail(contact.Email!, null);
        SetupLeadStoreSave();
        SetupStorageWriteImportSummary();

        await _sut.ExecuteAsync(AccountId, AgentId, [contact], Ct);

        _driveClient.Verify(d => d.CreateFolderAsync(
            AccountId, AgentId, "1 - Leads/Jane Doe", Ct), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Contact_WithEmail_SavesToLeadStore()
    {
        var contact = MakeContact("John Smith", "john@test.com", stage: PipelineStage.Lead);

        SetupDriveForContact(contact);
        SetupLeadStoreGetByEmail("john@test.com", null);
        SetupLeadStoreSave();
        SetupStorageWriteImportSummary();

        await _sut.ExecuteAsync(AccountId, AgentId, [contact], Ct);

        _leadStore.Verify(l => l.SaveAsync(
            It.Is<Lead>(lead => lead.Email == "john@test.com" && lead.AgentId == AgentId), Ct),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Contact_WithoutEmail_SkipsLeadStore()
    {
        var contact = MakeContact("No Email", null, stage: PipelineStage.Lead);

        SetupDriveForContact(contact);
        SetupStorageWriteImportSummary();

        await _sut.ExecuteAsync(AccountId, AgentId, [contact], Ct);

        _leadStore.Verify(l => l.SaveAsync(It.IsAny<Lead>(), Ct), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateContact_UpdatesStatusInsteadOfSave()
    {
        var contact = MakeContact("Existing Person", "existing@test.com", stage: PipelineStage.ActiveClient);

        var existingLead = MakeLead("existing@test.com");

        SetupDriveForContact(contact);
        SetupLeadStoreGetByEmail("existing@test.com", existingLead);
        _leadStore.Setup(l => l.UpdateStatusAsync(existingLead, LeadStatus.ActiveClient, Ct))
            .Returns(Task.CompletedTask);
        SetupStorageWriteImportSummary();

        await _sut.ExecuteAsync(AccountId, AgentId, [contact], Ct);

        _leadStore.Verify(l => l.UpdateStatusAsync(existingLead, LeadStatus.ActiveClient, Ct), Times.Once);
        _leadStore.Verify(l => l.SaveAsync(It.IsAny<Lead>(), Ct), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Contact_WithSingleWordName_SavesWithEmptyLastName()
    {
        var contact = MakeContact("Madonna", "madonna@test.com", stage: PipelineStage.Lead);

        SetupDriveForContact(contact);
        SetupLeadStoreGetByEmail("madonna@test.com", null);
        SetupLeadStoreSave();
        SetupStorageWriteImportSummary();

        await _sut.ExecuteAsync(AccountId, AgentId, [contact], Ct);

        _leadStore.Verify(l => l.SaveAsync(
            It.Is<Lead>(lead => lead.FirstName == "Madonna" && lead.LastName == string.Empty), Ct),
            Times.Once);
    }

    // ── ExecuteAsync: import summary ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WritesImportSummary_WhenNoExistingFile()
    {
        var contacts = new List<ImportedContact>();

        SetupDriveGetOrCreateFolder(Times.Exactly(5));
        _storage.Setup(s => s.ReadDocumentAsync(AgentId, ContactImportPersistActivity.ImportSummaryFile, Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(
            AgentId, ContactImportPersistActivity.ImportSummaryFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(AccountId, AgentId, contacts, Ct);

        _storage.Verify(s => s.WriteDocumentAsync(
            AgentId, ContactImportPersistActivity.ImportSummaryFile, It.IsAny<string>(), Ct), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesImportSummary_WhenExistingFileExists()
    {
        var contacts = new List<ImportedContact>();

        SetupDriveGetOrCreateFolder(Times.Exactly(5));
        _storage.Setup(s => s.ReadDocumentAsync(AgentId, ContactImportPersistActivity.ImportSummaryFile, Ct))
            .ReturnsAsync("old summary");
        _storage.Setup(s => s.UpdateDocumentAsync(
            AgentId, ContactImportPersistActivity.ImportSummaryFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(AccountId, AgentId, contacts, Ct);

        _storage.Verify(s => s.UpdateDocumentAsync(
            AgentId, ContactImportPersistActivity.ImportSummaryFile, It.IsAny<string>(), Ct), Times.Once);
        _storage.Verify(s => s.WriteDocumentAsync(
            AgentId, ContactImportPersistActivity.ImportSummaryFile, It.IsAny<string>(), Ct), Times.Never);
    }

    // ── ExecuteAsync: document copy routing ───────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Document_WithMappedSubFolder_CopiedToSubFolder()
    {
        var doc = new DocumentReference("file-id-1", "Agreement.pdf", DocumentType.ListingAgreement, null);
        var contact = MakeContact(
            "Alice Seller",
            "alice@test.com",
            stage: PipelineStage.ActiveClient,
            documents: [doc]);

        SetupDriveForContact(contact);
        // Expect GetOrCreateFolder for the agreements sub-folder under ActiveClient stage path
        _driveClient.Setup(d => d.GetOrCreateFolderAsync(
            AccountId, AgentId, "2 - Active Clients/Alice Seller/Agreements", Ct))
            .ReturnsAsync("agreements-folder-id");
        _driveClient.Setup(d => d.CopyFileAsync(
            AccountId, AgentId, "file-id-1", "agreements-folder-id", "Agreement.pdf", Ct))
            .ReturnsAsync("new-file-id");

        SetupLeadStoreGetByEmail("alice@test.com", null);
        SetupLeadStoreSave();
        SetupStorageWriteImportSummary();

        await _sut.ExecuteAsync(AccountId, AgentId, [contact], Ct);

        _driveClient.Verify(d => d.CopyFileAsync(
            AccountId, AgentId, "file-id-1", "agreements-folder-id", "Agreement.pdf", Ct), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Document_WithUnmappedSubFolder_NotCopied()
    {
        var doc = new DocumentReference("file-id-1", "Other.pdf", DocumentType.Other, null);
        var contact = MakeContact(
            "Bob Lead",
            "bob@test.com",
            stage: PipelineStage.Lead,
            documents: [doc]);

        SetupDriveForContact(contact);
        SetupLeadStoreGetByEmail("bob@test.com", null);
        SetupLeadStoreSave();
        SetupStorageWriteImportSummary();

        await _sut.ExecuteAsync(AccountId, AgentId, [contact], Ct);

        _driveClient.Verify(d => d.CopyFileAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), Ct), Times.Never);
    }

    // ── Constants ─────────────────────────────────────────────────────────────

    [Fact]
    public void ImportSummaryFile_IsCorrect()
    {
        ContactImportPersistActivity.ImportSummaryFile.Should().Be("Client Import Summary.md");
    }

    [Fact]
    public void TopLevelFolders_Contains5Folders()
    {
        ContactImportPersistActivity.TopLevelFolders.Should().HaveCount(5);
    }

    [Fact]
    public void TopLevelFolders_ContainsExpectedNames()
    {
        ContactImportPersistActivity.TopLevelFolders.Should().BeEquivalentTo(
        [
            "1 - Leads",
            "2 - Active Clients",
            "3 - Under Contract",
            "4 - Closed",
            "5 - Inactive"
        ]);
    }

    // ── Test helpers ──────────────────────────────────────────────────────────

    private static ImportedContact MakeContact(
        string name,
        string? email,
        ContactRole role = ContactRole.Buyer,
        PipelineStage stage = PipelineStage.Lead,
        string? propertyAddress = null,
        IReadOnlyList<DocumentReference>? documents = null)
    {
        return new ImportedContact(
            name,
            email,
            null,
            role,
            stage,
            propertyAddress,
            documents ?? []);
    }

    private static Lead MakeLead(string email) => new Lead
    {
        Id = Guid.NewGuid(),
        AgentId = AgentId,
        LeadType = LeadType.Buyer,
        FirstName = "Existing",
        LastName = "Contact",
        Email = email,
        Phone = string.Empty,
        Timeline = "Imported",
        Status = LeadStatus.Received,
    };

    private void SetupDriveGetOrCreateFolder(Times times)
    {
        _driveClient.Setup(d => d.GetOrCreateFolderAsync(
            AccountId, AgentId, It.IsAny<string>(), Ct))
            .ReturnsAsync("folder-id");
    }

    private void SetupDriveForContact(ImportedContact contact)
    {
        // Top-level folders (5) + stage folder for contact
        SetupDriveGetOrCreateFolder(Times.AtLeastOnce());

        // Contact folder creation
        var stageFolderName = ContactImportPersistActivity.GetStageFolderName(contact.Stage);
        _driveClient.Setup(d => d.CreateFolderAsync(
            AccountId, AgentId, $"{stageFolderName}/{contact.Name}", Ct))
            .ReturnsAsync("contact-folder-id");

        // Sub-folders for this stage
        var subFolders = ContactImportPersistActivity.GetStageSubFolders(contact.Stage, contact.PropertyAddress);
        foreach (var sub in subFolders)
        {
            _driveClient.Setup(d => d.CreateFolderAsync(
                AccountId, AgentId, $"{stageFolderName}/{contact.Name}/{sub}", Ct))
                .ReturnsAsync("sub-folder-id");
        }
    }

    private void SetupLeadStoreGetByEmail(string email, Lead? result)
    {
        _leadStore.Setup(l => l.GetByEmailAsync(AgentId, email, Ct))
            .ReturnsAsync(result);
    }

    private void SetupLeadStoreSave()
    {
        _leadStore.Setup(l => l.SaveAsync(It.IsAny<Lead>(), Ct))
            .Returns(Task.CompletedTask);
    }

    private void SetupStorageWriteImportSummary()
    {
        _storage.Setup(s => s.ReadDocumentAsync(
            AgentId, ContactImportPersistActivity.ImportSummaryFile, Ct))
            .ReturnsAsync((string?)null);
        _storage.Setup(s => s.WriteDocumentAsync(
            AgentId, ContactImportPersistActivity.ImportSummaryFile, It.IsAny<string>(), Ct))
            .Returns(Task.CompletedTask);
    }
}
