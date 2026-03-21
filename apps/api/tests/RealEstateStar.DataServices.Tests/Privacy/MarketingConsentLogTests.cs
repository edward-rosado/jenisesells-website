using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RealEstateStar.Domain.Privacy;

namespace RealEstateStar.DataServices.Tests.Privacy;

public class MarketingConsentLogTests
{
    private static MarketingConsentLog CreateConsentLog(IFileStorageProvider storageProvider, string secret = "test-secret")
    {
        var hmacOptions = Options.Create(new ConsentHmacOptions { Secret = secret });
        return new MarketingConsentLog(storageProvider, hmacOptions, NullLogger<MarketingConsentLog>.Instance);
    }

    [Fact]
    public async Task RecordConsentAsync_AppendRowAsync_WithAllThirteenColumns()
    {
        var storageProvider = new Mock<IFileStorageProvider>();
        var consentLog = CreateConsentLog(storageProvider.Object);

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
            Timestamp = timestamp,
            Action = ConsentAction.OptIn,
            Source = ConsentSource.LeadForm,
        };

        await consentLog.RecordConsentAsync("agent1", consent, CancellationToken.None);

        storageProvider.Verify(
            s => s.AppendRowAsync(
                LeadPaths.ConsentLogSheet,
                It.Is<List<string>>(values =>
                    values.Count == 13 &&
                    values[0] == timestamp.ToString("o") &&
                    values[1] == leadId.ToString() &&
                    values[2] == "john@example.com" &&
                    values[3] == "John" &&
                    values[4] == "Doe" &&
                    values[5] == "True" &&
                    values[6] == "User consented to marketing emails" &&
                    values[7] == "email,sms" &&
                    values[8] == "192.168.1.1" &&
                    values[9] == "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" &&
                    values[10] == "OptIn" &&
                    values[11] == "LeadForm" &&
                    values[12].StartsWith("sha256=")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RecordConsentAsync_TimestampIsUtcIso8601()
    {
        var storageProvider = new Mock<IFileStorageProvider>();
        var consentLog = CreateConsentLog(storageProvider.Object);

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
            Timestamp = timestamp,
            Action = ConsentAction.OptOut,
            Source = ConsentSource.EmailLink,
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
        var consentLog = CreateConsentLog(storageProvider.Object);

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
            Timestamp = timestamp,
            Action = ConsentAction.OptIn,
            Source = ConsentSource.LeadForm,
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
        var consentLog = CreateConsentLog(storageProvider.Object);

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
            Timestamp = timestamp,
            Action = ConsentAction.OptOut,
            Source = ConsentSource.EmailLink,
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

    [Fact]
    public async Task RecordConsentAsync_AppendsHmacSignatureAsLastColumn()
    {
        var storageProvider = new Mock<IFileStorageProvider>();
        var hmacOptions = Options.Create(new ConsentHmacOptions { Secret = "test-secret-key" });
        var consentLog = new MarketingConsentLog(storageProvider.Object, hmacOptions, NullLogger<MarketingConsentLog>.Instance);

        List<string>? capturedRow = null;
        storageProvider.Setup(sp => sp.AppendRowAsync(
            It.IsAny<string>(), It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, List<string>, CancellationToken>((_, row, _) => capturedRow = row)
            .Returns(Task.CompletedTask);

        var consent = new MarketingConsent
        {
            LeadId = Guid.NewGuid(), Email = "test@example.com",
            FirstName = "Test", LastName = "User", OptedIn = true,
            ConsentText = "Consented", Channels = ["email"],
            IpAddress = "127.0.0.1", UserAgent = "TestAgent",
            Timestamp = DateTime.UtcNow,
            Action = ConsentAction.OptIn, Source = ConsentSource.LeadForm,
        };

        await consentLog.RecordConsentAsync("agent-1", consent, CancellationToken.None);

        Assert.NotNull(capturedRow);
        Assert.Equal(13, capturedRow.Count);
        Assert.StartsWith("sha256=", capturedRow[^1]);
    }

    [Fact]
    public void ComputeHmacSignature_ReturnsSha256PrefixedHex()
    {
        var consent = new MarketingConsent
        {
            LeadId = Guid.NewGuid(),
            Email = "sign@example.com",
            FirstName = "Sign",
            LastName = "Test",
            OptedIn = true,
            ConsentText = "Consent",
            Channels = ["email"],
            IpAddress = "1.2.3.4",
            UserAgent = "Agent",
            Timestamp = new DateTime(2026, 3, 19, 12, 0, 0, DateTimeKind.Utc),
            Action = ConsentAction.OptIn,
            Source = ConsentSource.LeadForm,
        };

        var signature = MarketingConsentLog.ComputeHmacSignature(consent, "my-secret");

        signature.Should().StartWith("sha256=");
        signature.Should().HaveLength(7 + 64); // "sha256=" + 32 bytes hex = 64 chars
    }

    [Fact]
    public void ComputeHmacSignature_IsDeterministic()
    {
        var consent = new MarketingConsent
        {
            LeadId = new Guid("12345678-1234-1234-1234-123456789012"),
            Email = "det@example.com",
            FirstName = "Det",
            LastName = "Test",
            OptedIn = true,
            ConsentText = "Consent",
            Channels = ["sms"],
            IpAddress = "5.6.7.8",
            UserAgent = "Bot",
            Timestamp = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Action = ConsentAction.OptIn,
            Source = ConsentSource.LeadForm,
        };

        var sig1 = MarketingConsentLog.ComputeHmacSignature(consent, "secret");
        var sig2 = MarketingConsentLog.ComputeHmacSignature(consent, "secret");

        sig1.Should().Be(sig2);
    }

    [Fact]
    public void BuildCsvRow_Returns13Columns()
    {
        var consent = new MarketingConsent
        {
            LeadId = Guid.NewGuid(),
            Email = "row@example.com",
            FirstName = "Row",
            LastName = "Test",
            OptedIn = true,
            ConsentText = "Consent text",
            Channels = ["email"],
            IpAddress = "9.9.9.9",
            UserAgent = "TestBrowser",
            Timestamp = new DateTime(2026, 3, 19, 0, 0, 0, DateTimeKind.Utc),
            Action = ConsentAction.OptIn,
            Source = ConsentSource.LeadForm,
        };

        var row = MarketingConsentLog.BuildCsvRow(consent, "sha256=abc123");

        row.Should().HaveCount(13);
        row[10].Should().Be("OptIn");
        row[11].Should().Be("LeadForm");
        row[12].Should().Be("sha256=abc123");
    }
}
