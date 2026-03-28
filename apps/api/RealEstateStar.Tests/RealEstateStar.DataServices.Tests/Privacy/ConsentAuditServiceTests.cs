using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Domain.Privacy;

namespace RealEstateStar.DataServices.Tests.Privacy;

public class ConsentAuditServiceTests
{
    private readonly Mock<TableClient> _tableClient = new();

    private static MarketingConsent BuildConsent(string email = "test@example.com") =>
        new()
        {
            LeadId = Guid.NewGuid(),
            Email = email,
            FirstName = "Test",
            LastName = "User",
            OptedIn = true,
            ConsentText = "Consented",
            Channels = ["email", "calls"],
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent",
            Timestamp = DateTime.UtcNow,
            Action = ConsentAction.OptIn,
            Source = ConsentSource.LeadForm,
        };

    [Fact]
    public async Task RecordAsync_UpsertsEntityWithHashedEmail()
    {
        var sut = new ConsentAuditService(_tableClient.Object, NullLogger<ConsentAuditService>.Instance);
        var consent = BuildConsent("test@example.com");

        ConsentAuditEntry? captured = null;
        _tableClient.Setup(tc => tc.UpsertEntityAsync(
                It.IsAny<ConsentAuditEntry>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .Callback<ConsentAuditEntry, TableUpdateMode, CancellationToken>((entity, _, _) => captured = entity)
            .ReturnsAsync(Mock.Of<Response>());

        await sut.RecordAsync("agent-1", consent, "hmac-sig-123", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("agent-1", captured.PartitionKey);
        Assert.NotEmpty(captured.RowKey);
        Assert.DoesNotContain("test@example.com", captured.EmailHash, StringComparison.Ordinal);
        Assert.Equal("hmac-sig-123", captured.HmacSignature);
        Assert.Equal("OptIn", captured.Action);
    }

    [Fact]
    public async Task RecordAsync_RowKeyIsNewGuid()
    {
        var sut = new ConsentAuditService(_tableClient.Object, NullLogger<ConsentAuditService>.Instance);
        var consent = BuildConsent();

        string? capturedRowKey = null;
        _tableClient.Setup(tc => tc.UpsertEntityAsync(
                It.IsAny<ConsentAuditEntry>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .Callback<ConsentAuditEntry, TableUpdateMode, CancellationToken>((entity, _, _) => capturedRowKey = entity.RowKey)
            .ReturnsAsync(Mock.Of<Response>());

        await sut.RecordAsync("agent-1", consent, "sig", CancellationToken.None);

        Assert.NotNull(capturedRowKey);
        Assert.True(Guid.TryParse(capturedRowKey, out _), "RowKey should be a valid GUID");
    }

    [Fact]
    public async Task RecordAsync_ChannelsJoinedWithComma()
    {
        var sut = new ConsentAuditService(_tableClient.Object, NullLogger<ConsentAuditService>.Instance);
        var consent = BuildConsent();

        ConsentAuditEntry? captured = null;
        _tableClient.Setup(tc => tc.UpsertEntityAsync(
                It.IsAny<ConsentAuditEntry>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .Callback<ConsentAuditEntry, TableUpdateMode, CancellationToken>((entity, _, _) => captured = entity)
            .ReturnsAsync(Mock.Of<Response>());

        await sut.RecordAsync("agent-1", consent, "sig", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("email,calls", captured.Channels);
    }

    [Fact]
    public async Task RecordAsync_OptOutAction_StoredAsString()
    {
        var sut = new ConsentAuditService(_tableClient.Object, NullLogger<ConsentAuditService>.Instance);
        var consent = new MarketingConsent
        {
            LeadId = Guid.NewGuid(),
            Email = "x@y.com",
            FirstName = "A",
            LastName = "B",
            OptedIn = false,
            ConsentText = "Opted out",
            Channels = ["email"],
            IpAddress = "0",
            UserAgent = "U",
            Timestamp = DateTime.UtcNow,
            Action = ConsentAction.OptOut,
            Source = ConsentSource.EmailLink,
        };

        ConsentAuditEntry? captured = null;
        _tableClient.Setup(tc => tc.UpsertEntityAsync(
                It.IsAny<ConsentAuditEntry>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .Callback<ConsentAuditEntry, TableUpdateMode, CancellationToken>((entity, _, _) => captured = entity)
            .ReturnsAsync(Mock.Of<Response>());

        await sut.RecordAsync("agent-1", consent, "sig", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("OptOut", captured.Action);
        Assert.Equal("EmailLink", captured.Source);
    }

    [Fact]
    public async Task RecordAsync_DoesNotThrowOnTableClientFailure()
    {
        _tableClient.Setup(tc => tc.UpsertEntityAsync(
                It.IsAny<ConsentAuditEntry>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("table down"));

        var sut = new ConsentAuditService(_tableClient.Object, NullLogger<ConsentAuditService>.Instance);
        var consent = BuildConsent("x@y.com");

        await sut.RecordAsync("agent-1", consent, "sig", CancellationToken.None);
        // No exception — audit writes are non-blocking
    }

    [Fact]
    public void ComputeSha256_ReturnsDeterministicLowercaseHex()
    {
        var hash1 = ConsentAuditService.ComputeSha256("test@example.com");
        var hash2 = ConsentAuditService.ComputeSha256("test@example.com");

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // full SHA-256 hex = 32 bytes = 64 chars
        Assert.Equal(hash1, hash1.ToLowerInvariant());
    }

    [Fact]
    public void ComputeSha256_DifferentInputsProduceDifferentHashes()
    {
        var hash1 = ConsentAuditService.ComputeSha256("a@b.com");
        var hash2 = ConsentAuditService.ComputeSha256("c@d.com");

        Assert.NotEqual(hash1, hash2);
    }
}
