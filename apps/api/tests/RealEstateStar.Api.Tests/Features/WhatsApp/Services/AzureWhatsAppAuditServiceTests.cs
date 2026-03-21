using Azure;
using Azure.Data.Tables;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.DataServices.WhatsApp;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Services;

public class AzureWhatsAppAuditServiceTests
{
    private readonly Mock<TableClient> _tableClient = new();
    private readonly AzureWhatsAppAuditService _sut;

    public AzureWhatsAppAuditServiceTests()
    {
        _sut = new AzureWhatsAppAuditService(_tableClient.Object,
            Mock.Of<ILogger<AzureWhatsAppAuditService>>());
    }

    [Fact]
    public async Task RecordReceivedAsync_WritesEntryWithReceivedStatus()
    {
        await _sut.RecordReceivedAsync("wamid.abc", "+12015551234", "PHONE_ID",
            "What's her budget?", "text", CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.RowKey == "wamid.abc" &&
                e.FromPhone == "+12015551234" &&
                e.ProcessingStatus == "received"),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProcessingAsync_SetsAgentIdAndStatus()
    {
        await _sut.UpdateProcessingAsync("wamid.abc", "agent-1", CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.RowKey == "wamid.abc" &&
                e.AgentId == "agent-1" &&
                e.ProcessingStatus == "processing"),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateCompletedAsync_SetsResponseAndTimestamp()
    {
        await _sut.UpdateCompletedAsync("wamid.abc", "agent-1", "LeadQuestion",
            "Her budget is $650K.", CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.ProcessingStatus == "completed" &&
                e.IntentClassification == "LeadQuestion" &&
                e.ResponseSent == "Her budget is $650K." &&
                e.ProcessedAt != null),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateFailedAsync_SetsErrorDetails()
    {
        await _sut.UpdateFailedAsync("wamid.abc", "agent-1", "Claude API timeout",
            CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.ProcessingStatus == "failed" &&
                e.ErrorDetails == "Claude API timeout"),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RecordReceivedAsync_DoesNotThrow_OnTableStorageFailure()
    {
        _tableClient.Setup(t => t.UpsertEntityAsync(
            It.IsAny<WhatsAppAuditEntry>(), It.IsAny<TableUpdateMode>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("Table unavailable"));

        var act = () => _sut.RecordReceivedAsync("wamid.abc", "+12015551234", "PHONE_ID",
            "Hi", "text", CancellationToken.None);

        await act.Should().NotThrowAsync();
        // Logger should have [WA-024] warning
    }

    [Fact]
    public async Task UpdatePoisonAsync_SetsUnknownPartitionAndPoisonStatus()
    {
        await _sut.UpdatePoisonAsync("wamid.abc", "Exceeded retry limit", CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.PartitionKey == "unknown" &&
                e.RowKey == "wamid.abc" &&
                e.ProcessingStatus == "poison" &&
                e.ErrorDetails == "Exceeded retry limit"),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateFailedAsync_NullAgentId_UsesUnknownPartition()
    {
        await _sut.UpdateFailedAsync("wamid.abc", null, "Agent not found", CancellationToken.None);

        _tableClient.Verify(t => t.UpsertEntityAsync(
            It.Is<WhatsAppAuditEntry>(e =>
                e.PartitionKey == "unknown" &&
                e.ProcessingStatus == "failed"),
            TableUpdateMode.Merge, It.IsAny<CancellationToken>()), Times.Once);
    }
}
