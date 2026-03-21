using Azure.Data.Tables;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.DataServices.Leads;

namespace RealEstateStar.DataServices.Tests.Leads;

public class FailedNotificationStoreTests
{
    private readonly Mock<TableClient> _tableClient = new();

    [Fact]
    public async Task RecordAsync_UpsertsEntityWithLeadDetails()
    {
        var sut = new FailedNotificationStore(_tableClient.Object, NullLogger<FailedNotificationStore>.Instance);

        FailedNotificationEntry? captured = null;
        _tableClient.Setup(tc => tc.UpsertEntityAsync(
            It.IsAny<FailedNotificationEntry>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .Callback<FailedNotificationEntry, TableUpdateMode, CancellationToken>((e, _, _) => captured = e)
            .ReturnsAsync(Mock.Of<Azure.Response>());

        var leadId = Guid.NewGuid();
        await sut.RecordAsync("agent-1", leadId, "All channels failed", 3, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("agent-1", captured.PartitionKey);
        Assert.Equal(leadId, captured.LeadId);
        Assert.Equal("All channels failed", captured.LastError);
        Assert.Equal(3, captured.RetryCount);
    }

    [Fact]
    public async Task RecordAsync_DoesNotThrowOnTableClientFailure()
    {
        _tableClient.Setup(tc => tc.UpsertEntityAsync(
            It.IsAny<FailedNotificationEntry>(), It.IsAny<TableUpdateMode>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Azure.RequestFailedException("table down"));

        var sut = new FailedNotificationStore(_tableClient.Object, NullLogger<FailedNotificationStore>.Instance);
        var leadId = Guid.NewGuid();

        // Should not throw — dead letter writes are non-blocking
        await sut.RecordAsync("agent-1", leadId, "error", 1, CancellationToken.None);
    }
}
