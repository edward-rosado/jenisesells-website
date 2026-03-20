using FluentAssertions;
using Moq;
using RealEstateStar.Api.Features.Leads;
using RealEstateStar.Api.Features.Leads.Services;
using RealEstateStar.Api.Services.Storage;

namespace RealEstateStar.Api.Tests.Features.Leads.Services;

public class MarketingConsentLogTests
{
    [Fact]
    public async Task RecordConsentAsync_AppendRowAsync_WithAllTenColumns()
    {
        var storageProvider = new Mock<IFileStorageProvider>();
        var consentLog = new MarketingConsentLog(storageProvider.Object);

        var leadId = Guid.NewGuid();
        var timestamp = new DateTime(2026, 3, 19, 15, 30, 45, DateTimeKind.Utc);
        var consent = new MarketingConsent
        {
            LeadId = leadId,
            Email = "john@example.com",
            FirstName = "John",
            LastName = "Doe",
            OptedIn = true,
            ConsentText = "User consented to marketing emails",
            Channels = ["email", "sms"],
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            Timestamp = timestamp
        };

        await consentLog.RecordConsentAsync("agent1", consent, CancellationToken.None);

        storageProvider.Verify(
            s => s.AppendRowAsync(
                LeadPaths.ConsentLogSheet,
                It.Is<List<string>>(values =>
                    values.Count == 10 &&
                    values[0] == timestamp.ToString("o") &&
                    values[1] == leadId.ToString() &&
                    values[2] == "john@example.com" &&
                    values[3] == "John" &&
                    values[4] == "Doe" &&
                    values[5] == "True" &&
                    values[6] == "User consented to marketing emails" &&
                    values[7] == "email,sms" &&
                    values[8] == "192.168.1.1" &&
                    values[9] == "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordConsentAsync_TimestampIsUtcIso8601()
    {
        var storageProvider = new Mock<IFileStorageProvider>();
        var consentLog = new MarketingConsentLog(storageProvider.Object);

        var leadId = Guid.NewGuid();
        var timestamp = new DateTime(2026, 3, 19, 15, 30, 45, DateTimeKind.Utc);
        var consent = new MarketingConsent
        {
            LeadId = leadId,
            Email = "jane@example.com",
            FirstName = "Jane",
            LastName = "Smith",
            OptedIn = false,
            ConsentText = "User declined marketing emails",
            Channels = [],
            IpAddress = "10.0.0.1",
            UserAgent = "Mozilla/5.0",
            Timestamp = timestamp
        };

        await consentLog.RecordConsentAsync("agent1", consent, CancellationToken.None);

        storageProvider.Verify(
            s => s.AppendRowAsync(
                LeadPaths.ConsentLogSheet,
                It.Is<List<string>>(values =>
                    values[0] == "2026-03-19T15:30:45.0000000Z"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordConsentAsync_ChannelsAreCommaSeparated()
    {
        var storageProvider = new Mock<IFileStorageProvider>();
        var consentLog = new MarketingConsentLog(storageProvider.Object);

        var leadId = Guid.NewGuid();
        var timestamp = new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc);
        var consent = new MarketingConsent
        {
            LeadId = leadId,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            OptedIn = true,
            ConsentText = "Consent given",
            Channels = ["email", "sms", "push"],
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent",
            Timestamp = timestamp
        };

        await consentLog.RecordConsentAsync("agent1", consent, CancellationToken.None);

        storageProvider.Verify(
            s => s.AppendRowAsync(
                LeadPaths.ConsentLogSheet,
                It.Is<List<string>>(values =>
                    values[7] == "email,sms,push"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordConsentAsync_WithEmptyChannels()
    {
        var storageProvider = new Mock<IFileStorageProvider>();
        var consentLog = new MarketingConsentLog(storageProvider.Object);

        var leadId = Guid.NewGuid();
        var timestamp = new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc);
        var consent = new MarketingConsent
        {
            LeadId = leadId,
            Email = "nochannel@example.com",
            FirstName = "No",
            LastName = "Channel",
            OptedIn = false,
            ConsentText = "No channels",
            Channels = [],
            IpAddress = "192.168.0.1",
            UserAgent = "TestAgent",
            Timestamp = timestamp
        };

        await consentLog.RecordConsentAsync("agent1", consent, CancellationToken.None);

        storageProvider.Verify(
            s => s.AppendRowAsync(
                LeadPaths.ConsentLogSheet,
                It.Is<List<string>>(values =>
                    values[7] == ""),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
