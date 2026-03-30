using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Workers.Activation.EmailFetch.Tests;

public class AgentEmailFetchWorkerTests
{
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";

    private static AgentEmailFetchWorker BuildWorker(Mock<IGmailReader>? mockReader = null)
    {
        mockReader ??= new Mock<IGmailReader>();
        return new AgentEmailFetchWorker(
            mockReader.Object,
            NullLogger<AgentEmailFetchWorker>.Instance);
    }

    private static EmailMessage MakeEmail(string id, string body, DateTime? date = null) =>
        new(id, $"Subject-{id}", body, "agent@example.com", ["buyer@example.com"],
            date ?? DateTime.UtcNow, null);

    // ──────────────────────────────────────────────────────────
    // RunAsync — fetch counts
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_Fetches100SentAnd100Inbox()
    {
        var mockReader = new Mock<IGmailReader>();
        mockReader
            .Setup(r => r.GetSentEmailsAsync(AccountId, AgentId, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());
        mockReader
            .Setup(r => r.GetInboxEmailsAsync(AccountId, AgentId, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());

        var worker = BuildWorker(mockReader);
        await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        mockReader.Verify(r => r.GetSentEmailsAsync(AccountId, AgentId, 100, It.IsAny<CancellationToken>()), Times.Once);
        mockReader.Verify(r => r.GetInboxEmailsAsync(AccountId, AgentId, 100, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ReturnsEmptyCorpus_WhenMailboxEmpty()
    {
        var mockReader = new Mock<IGmailReader>();
        mockReader
            .Setup(r => r.GetSentEmailsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());
        mockReader
            .Setup(r => r.GetInboxEmailsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());

        var worker = BuildWorker(mockReader);
        var corpus = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        corpus.SentEmails.Should().BeEmpty();
        corpus.InboxEmails.Should().BeEmpty();
        corpus.Signature.Should().BeNull();
    }

    [Fact]
    public async Task RunAsync_ExtractsMostCommonSignatureFromSent()
    {
        var sig = "Best regards,\nJane Doe\nREALTOR\n555-123-4567\nhttps://janedoe.com";
        var emails = new List<EmailMessage>
        {
            MakeEmail("1", $"Hi there!\n\n{sig}"),
            MakeEmail("2", $"Follow up.\n\n{sig}"),
            MakeEmail("3", $"Another email.\n\n{sig}"),
        };

        var mockReader = new Mock<IGmailReader>();
        mockReader
            .Setup(r => r.GetSentEmailsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(emails);
        mockReader
            .Setup(r => r.GetInboxEmailsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());

        var worker = BuildWorker(mockReader);
        var corpus = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        corpus.Signature.Should().NotBeNull();
        corpus.Signature!.Phone.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunAsync_StripsQuotedRepliesFromEmails()
    {
        var emailWithQuote = MakeEmail("1",
            "Sure, sounds good!\n\nOn Mon, Jan 1, wrote:\n> Original message\n> continues here");

        var mockReader = new Mock<IGmailReader>();
        mockReader
            .Setup(r => r.GetSentEmailsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage> { emailWithQuote });
        mockReader
            .Setup(r => r.GetInboxEmailsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EmailMessage>());

        var worker = BuildWorker(mockReader);
        var corpus = await worker.RunAsync(AccountId, AgentId, CancellationToken.None);

        corpus.SentEmails.Should().ContainSingle();
        corpus.SentEmails[0].Body.Should().Contain("Sure, sounds good!");
        corpus.SentEmails[0].Body.Should().NotContain("> Original message");
    }

    // ──────────────────────────────────────────────────────────
    // ExtractSignature
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ExtractSignature_ReturnsNull_WhenNoEmails()
    {
        var result = AgentEmailFetchWorker.ExtractSignature([]);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractSignature_ParsesPhoneFromBlock()
    {
        var emails = new List<EmailMessage>
        {
            MakeEmail("1", "Hello\n\nBest regards,\nJane Doe\n555-987-6543\nJane Doe Realty")
        };

        var sig = AgentEmailFetchWorker.ExtractSignature(emails);

        sig.Should().NotBeNull();
        sig!.Phone.Should().Be("555-987-6543");
    }

    [Fact]
    public void ExtractSignature_ParsesWebsiteUrl()
    {
        var emails = new List<EmailMessage>
        {
            MakeEmail("1", "Thanks!\n\nBest regards,\nJane Doe\nhttps://www.janedoerealty.com\nREALTOR")
        };

        var sig = AgentEmailFetchWorker.ExtractSignature(emails);

        sig.Should().NotBeNull();
        sig!.WebsiteUrl.Should().Contain("janedoerealty.com");
    }

    [Fact]
    public void ExtractSignature_ParsesSocialLinks()
    {
        var emails = new List<EmailMessage>
        {
            MakeEmail("1", "See you soon!\n\nBest,\nJane\nhttps://linkedin.com/in/janedoe\nhttps://facebook.com/janedoe")
        };

        var sig = AgentEmailFetchWorker.ExtractSignature(emails);

        sig.Should().NotBeNull();
        sig!.SocialLinks.Should().HaveCountGreaterThan(0);
    }

    // ──────────────────────────────────────────────────────────
    // ExtractSignatureBlock
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ExtractSignatureBlock_ReturnsNull_WhenBodyEmpty()
    {
        var result = AgentEmailFetchWorker.ExtractSignatureBlock(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractSignatureBlock_ExtractsAfterDoubleDash()
    {
        var body = "Hello there.\n\n--\nJane Doe\nREALTOR";

        var result = AgentEmailFetchWorker.ExtractSignatureBlock(body);

        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("Jane Doe");
    }

    [Fact]
    public void ExtractSignatureBlock_ExtractsAfterBestRegards()
    {
        var body = "Thanks for reaching out!\n\nBest regards,\nJane Doe\n555-000-1234";

        var result = AgentEmailFetchWorker.ExtractSignatureBlock(body);

        result.Should().NotBeNullOrWhiteSpace();
        result.Should().Contain("Jane Doe");
    }

    // ──────────────────────────────────────────────────────────
    // StripQuotedReplies
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void StripQuotedReplies_RemovesGtLines()
    {
        var body = "My reply.\n\n> Original line\n> More original";

        var result = AgentEmailFetchWorker.StripQuotedReplies(body);

        result.Should().Contain("My reply.");
        result.Should().NotContain("> Original line");
    }

    [Fact]
    public void StripQuotedReplies_RemovesOnDateWroteBlock()
    {
        var body = "Sure!\n\nOn Mon, Jan 6, 2025, Agent wrote:\n> Let me know.";

        var result = AgentEmailFetchWorker.StripQuotedReplies(body);

        result.Should().Contain("Sure!");
        result.Should().NotContain("Agent wrote:");
        result.Should().NotContain("> Let me know.");
    }

    [Fact]
    public void StripQuotedReplies_PreservesBodyWithNoQuotes()
    {
        var body = "Hello, this is a normal email with no quoted replies.";

        var result = AgentEmailFetchWorker.StripQuotedReplies(body);

        result.Should().Be(body);
    }

    // ──────────────────────────────────────────────────────────
    // ParseSignature
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ParseSignature_ExtractsBrokerageName()
    {
        var block = "Jane Doe\nREALTOR Associate\n555-555-5555\nSunrise Realty Group\nhttps://sunriserealty.com";

        var sig = AgentEmailFetchWorker.ParseSignature(block);

        sig.BrokerageName.Should().Contain("Sunrise Realty Group");
    }

    [Fact]
    public void ParseSignature_ExtractsLicenseNumber()
    {
        var block = "Jane Doe\nLicense #NJ1234567\n555-000-1111";

        var sig = AgentEmailFetchWorker.ParseSignature(block);

        sig.LicenseNumber.Should().Be("NJ1234567");
    }

    [Fact]
    public void ParseSignature_ReturnsEmptySocialLinksWhenNone()
    {
        var block = "Jane Doe\n555-555-1234\nREALTOR";

        var sig = AgentEmailFetchWorker.ParseSignature(block);

        sig.SocialLinks.Should().BeEmpty();
    }
}
