using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.DataServices.Leads;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace RealEstateStar.DataServices.Tests.Leads;

public class LeadDataDeletionTests
{
    private readonly Mock<ILeadStore> _leadStore = new();
    private readonly Mock<IMarketingConsentLog> _consentLog = new();
    private readonly Mock<IDeletionAuditLog> _auditLog = new();
    private readonly Mock<IFileStorageProvider> _storage = new();
    private readonly Mock<IGwsService> _gws = new();
    private readonly Mock<ILogger<LeadDataDeletion>> _logger = new();
    private readonly LeadDataDeletion _sut;

    private const string AgentId = "jenise-buckalew";
    private const string LeadEmail = "jane.doe@example.com";

    public LeadDataDeletionTests()
    {
        _sut = new LeadDataDeletion(
            _leadStore.Object,
            _consentLog.Object,
            _auditLog.Object,
            _storage.Object,
            _gws.Object,
            _logger.Object);

        // Default setups so tests only need to override what they care about
        _storage.Setup(s => s.EnsureFolderExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.WriteDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.DeleteDocumentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _storage.Setup(s => s.ListDocumentsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _gws.Setup(g => g.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _auditLog.Setup(a => a.RecordInitiationAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _auditLog.Setup(a => a.RecordCompletionAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _consentLog.Setup(c => c.RedactAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _leadStore.Setup(s => s.GetByEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);
    }

    // ── Test data helpers ─────────────────────────────────────────────────────

    private static Lead MakeLead(
        string firstName = "Jane",
        string lastName = "Doe",
        string email = LeadEmail) => new()
        {
            Id = Guid.NewGuid(),
            AgentId = AgentId,
            LeadType = LeadType.Buyer,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            Phone = "5551234567",
            Timeline = "1-3months",
            Status = LeadStatus.Received,
            ReceivedAt = new DateTime(2026, 3, 19, 14, 0, 0, DateTimeKind.Utc),
        };

    private string BuildValidTokenJson(string email = LeadEmail, double hoursFromNow = 23)
    {
        var tokenData = new { Email = email, ExpiresAt = DateTime.UtcNow.AddHours(hoursFromNow), AgentId };
        return JsonSerializer.Serialize(tokenData);
    }

    private void SetupValidToken(string token, string email = LeadEmail, double hoursFromNow = 23)
    {
        var tokenHash = LeadDataDeletion.ComputeTokenHash(token);
        var tokenFolder = LeadPaths.DeletionTokensFolder(AgentId);
        _storage.Setup(s => s.ReadDocumentAsync(tokenFolder, $"{tokenHash}.json", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildValidTokenJson(email, hoursFromNow));
    }

    // ── InitiateDeletionRequestAsync ──────────────────────────────────────────

    [Fact]
    public async Task InitiateDeletionRequestAsync_GeneratesCryptographicallyRandomToken()
    {
        // Arrange: call twice and verify tokens differ (random)
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLead());

        // Act
        var token1 = await _sut.InitiateDeletionRequestAsync(AgentId, LeadEmail, CancellationToken.None);
        var token2 = await _sut.InitiateDeletionRequestAsync(AgentId, LeadEmail, CancellationToken.None);

        // Assert
        token1.Should().NotBeNullOrWhiteSpace();
        token2.Should().NotBeNullOrWhiteSpace();
        token1.Should().NotBe(token2, "tokens must be cryptographically random");
    }

    [Fact]
    public async Task InitiateDeletionRequestAsync_TokenIsBase64UrlEncoded()
    {
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLead());

        var token = await _sut.InitiateDeletionRequestAsync(AgentId, LeadEmail, CancellationToken.None);

        // Base64Url: no +, /, or = padding
        token.Should().NotContain("+");
        token.Should().NotContain("/");
        token.Should().NotContain("=");
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task InitiateDeletionRequestAsync_SendsVerificationEmailToLeadEmail()
    {
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLead());

        await _sut.InitiateDeletionRequestAsync(AgentId, LeadEmail, CancellationToken.None);

        _gws.Verify(g => g.SendEmailAsync(
            AgentId,
            LeadEmail,
            It.Is<string>(s => s.Contains("Deletion") || s.Contains("deletion")),
            It.IsAny<string>(),
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiateDeletionRequestAsync_EmailBodyContainsToken()
    {
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLead());

        string? capturedBody = null;
        string? returnedToken = null;
        _gws.Setup(g => g.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .Callback<string, string, string, string, string?, CancellationToken>((_, _, _, body, _, _) => capturedBody = body)
            .Returns(Task.CompletedTask);

        returnedToken = await _sut.InitiateDeletionRequestAsync(AgentId, LeadEmail, CancellationToken.None);

        capturedBody.Should().Contain(returnedToken);
    }

    [Fact]
    public async Task InitiateDeletionRequestAsync_RecordsInitiationInAuditLog()
    {
        var lead = MakeLead();
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        await _sut.InitiateDeletionRequestAsync(AgentId, LeadEmail, CancellationToken.None);

        _auditLog.Verify(a => a.RecordInitiationAsync(
            AgentId,
            lead.Id,
            LeadEmail,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitiateDeletionRequestAsync_StoresHashedTokenInStorage()
    {
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLead());

        string? storedContent = null;
        string? storedFileName = null;
        _storage.Setup(s => s.WriteDocumentAsync(
                It.Is<string>(f => f.Contains("Deletion Tokens")),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, fileName, content, _) =>
            {
                storedFileName = fileName;
                storedContent = content;
            })
            .Returns(Task.CompletedTask);

        var token = await _sut.InitiateDeletionRequestAsync(AgentId, LeadEmail, CancellationToken.None);

        // Verify the token itself is NOT stored (only the hash)
        storedContent.Should().NotContain(token, "raw token must never be stored");
        storedContent.Should().Contain(LeadEmail, "stored data must include the email");

        // Verify filename is the hash of the token
        var expectedHash = LeadDataDeletion.ComputeTokenHash(token);
        storedFileName.Should().Be($"{expectedHash}.json");
    }

    // ── ExecuteDeletionAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteDeletionAsync_DeletesLeadProfileMd()
    {
        var lead = MakeLead();
        var token = "test-token-value";
        SetupValidToken(token);
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var result = await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, token, "user request", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DeletedItems.Should().Contain("Lead Profile.md");

        var leadFolder = LeadPaths.LeadFolder(lead.FullName);
        _storage.Verify(s => s.DeleteDocumentAsync(leadFolder, "Lead Profile.md", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteDeletionAsync_DeletesResearchAndInsightsMd()
    {
        var lead = MakeLead();
        var token = "test-token-insights";
        SetupValidToken(token);
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var result = await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, token, "user request", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DeletedItems.Should().Contain("Research & Insights.md");

        var leadFolder = LeadPaths.LeadFolder(lead.FullName);
        _storage.Verify(s => s.DeleteDocumentAsync(leadFolder, "Research & Insights.md", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteDeletionAsync_DeletesHomeSearchFiles()
    {
        var lead = MakeLead();
        var token = "test-token-homesearch";
        SetupValidToken(token);
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var leadFolder = LeadPaths.LeadFolder(lead.FullName);
        var homeSearchFolder = $"{leadFolder}/Home Search";
        _storage.Setup(s => s.ListDocumentsAsync(homeSearchFolder, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["2026-03-01-Home Search Results.md", "2026-03-15-Home Search Results.md"]);

        var result = await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, token, "user request", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DeletedItems.Should().Contain("Home Search/2026-03-01-Home Search Results.md");
        result.DeletedItems.Should().Contain("Home Search/2026-03-15-Home Search Results.md");

        _storage.Verify(s => s.DeleteDocumentAsync(homeSearchFolder, "2026-03-01-Home Search Results.md", It.IsAny<CancellationToken>()), Times.Once);
        _storage.Verify(s => s.DeleteDocumentAsync(homeSearchFolder, "2026-03-15-Home Search Results.md", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteDeletionAsync_RedactsConsentLogRows()
    {
        var lead = MakeLead();
        var token = "test-token-consent";
        SetupValidToken(token);
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var result = await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, token, "user request", CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DeletedItems.Should().Contain(s => s.Contains("Consent log"));

        _consentLog.Verify(c => c.RedactAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteDeletionAsync_RecordsCompletionInAuditLog()
    {
        var lead = MakeLead();
        var token = "test-token-audit";
        SetupValidToken(token);
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        var result = await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, token, "user request", CancellationToken.None);

        result.Success.Should().BeTrue();
        _auditLog.Verify(a => a.RecordCompletionAsync(AgentId, lead.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteDeletionAsync_RejectsExpiredToken()
    {
        var token = "expired-token";
        SetupValidToken(token, LeadEmail, hoursFromNow: -1); // 1 hour in the past

        var result = await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, token, "user request", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("expired");
        result.DeletedItems.Should().BeEmpty();

        _leadStore.Verify(s => s.GetByEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteDeletionAsync_RejectsInvalidToken()
    {
        // No token stored for this hash
        var tokenFolder = LeadPaths.DeletionTokensFolder(AgentId);
        _storage.Setup(s => s.ReadDocumentAsync(It.Is<string>(f => f.Contains("Deletion Tokens")), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var result = await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, "invalid-token", "user request", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.DeletedItems.Should().BeEmpty();

        _leadStore.Verify(s => s.GetByEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteDeletionAsync_ReturnsErrorForAlreadyDeletedLead()
    {
        var token = "test-token-nodlead";
        SetupValidToken(token);
        // Lead not found (already deleted)
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        var result = await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, token, "user request", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
        result.DeletedItems.Should().BeEmpty();

        _auditLog.Verify(a => a.RecordCompletionAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteDeletionAsync_AuditLogRecordsCompletionWithRedactedEmail()
    {
        // The DeletionAuditLog.RecordCompletionAsync records [REDACTED] — verify our impl calls RecordCompletion (not Initiation)
        var lead = MakeLead();
        var token = "test-token-redact-check";
        SetupValidToken(token);
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, token, "user request", CancellationToken.None);

        // Verify we call RecordCompletionAsync (which internally redacts the email per DeletionAuditLog impl)
        _auditLog.Verify(a => a.RecordCompletionAsync(AgentId, lead.Id, It.IsAny<CancellationToken>()), Times.Once);
        // We must NOT call RecordInitiationAsync on completion
        _auditLog.Verify(a => a.RecordInitiationAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteDeletionAsync_RejectsTokenWithMismatchedEmail()
    {
        var token = "test-token-mismatch";
        // Token was issued for a different email
        SetupValidToken(token, "different@example.com");
        _leadStore.Setup(s => s.GetByEmailAsync(AgentId, LeadEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeLead());

        var result = await _sut.ExecuteDeletionAsync(AgentId, LeadEmail, token, "user request", CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ComputeTokenHash_IsDeterministic()
    {
        var token = "my-test-token";
        var hash1 = LeadDataDeletion.ComputeTokenHash(token);
        var hash2 = LeadDataDeletion.ComputeTokenHash(token);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public async Task ComputeTokenHash_DifferentTokensProduceDifferentHashes()
    {
        var hash1 = LeadDataDeletion.ComputeTokenHash("token-a");
        var hash2 = LeadDataDeletion.ComputeTokenHash("token-b");

        hash1.Should().NotBe(hash2);
    }
}
