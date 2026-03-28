using Azure.Data.Tables;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealEstateStar.DataServices.Tests.Privacy;

public class ConsentTokenStoreTests
{
    private readonly Mock<TableClient> _tableClient = new();

    [Fact]
    public async Task StoreAsync_UpsertsEntryWithTokenHashAsRowKey()
    {
        var sut = new ConsentTokenStore(_tableClient.Object, NullLogger<ConsentTokenStore>.Instance);
        ConsentTokenEntry? captured = null;
        _tableClient.Setup(tc => tc.UpsertEntityAsync(
            It.IsAny<ConsentTokenEntry>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .Callback<ConsentTokenEntry, TableUpdateMode, CancellationToken>((e, _, _) => captured = e)
            .ReturnsAsync(Mock.Of<Azure.Response>());

        var leadId = Guid.NewGuid();
        await sut.StoreAsync("agent-1", "abc123hash", leadId, "test@example.com", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("agent-1", captured.PartitionKey);
        Assert.Equal("abc123hash", captured.RowKey);
        Assert.Equal(leadId, captured.LeadId);
    }

    [Fact]
    public async Task StoreAsync_HashesEmailBeforeStoring()
    {
        var sut = new ConsentTokenStore(_tableClient.Object, NullLogger<ConsentTokenStore>.Instance);
        ConsentTokenEntry? captured = null;
        _tableClient.Setup(tc => tc.UpsertEntityAsync(
            It.IsAny<ConsentTokenEntry>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .Callback<ConsentTokenEntry, TableUpdateMode, CancellationToken>((e, _, _) => captured = e)
            .ReturnsAsync(Mock.Of<Azure.Response>());

        await sut.StoreAsync("agent-1", "abc123hash", Guid.NewGuid(), "test@example.com", CancellationToken.None);

        Assert.NotNull(captured);
        Assert.DoesNotContain("test@example.com", captured.EmailHash, StringComparison.Ordinal);
        Assert.Equal(64, captured.EmailHash.Length); // SHA-256 hex = 64 chars
    }

    [Fact]
    public async Task LookupAsync_ReturnsLeadIdWhenTokenExists()
    {
        var entry = new ConsentTokenEntry
        {
            PartitionKey = "agent-1",
            RowKey = "abc123hash",
            LeadId = Guid.NewGuid(),
            EmailHash = "emailhash"
        };
        _tableClient.Setup(tc => tc.GetEntityAsync<ConsentTokenEntry>(
            "agent-1", "abc123hash", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Azure.Response.FromValue(entry, Mock.Of<Azure.Response>()));

        var sut = new ConsentTokenStore(_tableClient.Object, NullLogger<ConsentTokenStore>.Instance);
        var result = await sut.LookupAsync("agent-1", "abc123hash", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(entry.LeadId, result.Value.LeadId);
    }

    [Fact]
    public async Task LookupAsync_ReturnsEmailHashWhenTokenExists()
    {
        var entry = new ConsentTokenEntry
        {
            PartitionKey = "agent-1",
            RowKey = "abc123hash",
            LeadId = Guid.NewGuid(),
            EmailHash = "myhash"
        };
        _tableClient.Setup(tc => tc.GetEntityAsync<ConsentTokenEntry>(
            "agent-1", "abc123hash", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Azure.Response.FromValue(entry, Mock.Of<Azure.Response>()));

        var sut = new ConsentTokenStore(_tableClient.Object, NullLogger<ConsentTokenStore>.Instance);
        var result = await sut.LookupAsync("agent-1", "abc123hash", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("myhash", result.Value.EmailHash);
    }

    [Fact]
    public async Task LookupAsync_ReturnsNullWhenTokenNotFound()
    {
        _tableClient.Setup(tc => tc.GetEntityAsync<ConsentTokenEntry>(
            "agent-1", "missing", It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Azure.RequestFailedException(404, "Not found"));

        var sut = new ConsentTokenStore(_tableClient.Object, NullLogger<ConsentTokenStore>.Instance);
        var result = await sut.LookupAsync("agent-1", "missing", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeAsync_DeletesEntryFromTable()
    {
        var sut = new ConsentTokenStore(_tableClient.Object, NullLogger<ConsentTokenStore>.Instance);
        await sut.RevokeAsync("agent-1", "abc123hash", CancellationToken.None);

        _tableClient.Verify(tc => tc.DeleteEntityAsync(
            "agent-1", "abc123hash", It.IsAny<Azure.ETag>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAsync_DoesNotThrowWhenAlreadyRevoked()
    {
        _tableClient.Setup(tc => tc.DeleteEntityAsync(
            "agent-1", "abc123hash", It.IsAny<Azure.ETag>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Azure.RequestFailedException(404, "Not found"));

        var sut = new ConsentTokenStore(_tableClient.Object, NullLogger<ConsentTokenStore>.Instance);
        // Should not throw — idempotent
        await sut.RevokeAsync("agent-1", "abc123hash", CancellationToken.None);
    }
}
