using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.Workers.Activation.EmailClassification.Tests;

public class EmailClassificationWorkerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static EmailMessage MakeEmail(
        string id = "msg-001",
        string subject = "Open House This Weekend",
        string body = "Join us for an open house at 123 Main St.",
        string from = "agent@example.com",
        DateTime? date = null)
        => new(
            Id: id,
            Subject: subject,
            Body: body,
            From: from,
            To: ["client@example.com"],
            Date: date ?? DateTime.UtcNow,
            SignatureBlock: null,
            DetectedLocale: null);

    private static EmailCorpus MakeCorpus(
        IReadOnlyList<EmailMessage>? sent = null,
        IReadOnlyList<EmailMessage>? inbox = null)
        => new(
            SentEmails: sent ?? new List<EmailMessage>(),
            InboxEmails: inbox ?? new List<EmailMessage>(),
            Signature: null);

    private static AnthropicResponse MakeValidClassificationResponse() =>
        new(Content: """
            {
              "classifications": [
                {"id": "msg-001", "categories": ["Marketing"]},
                {"id": "msg-002", "categories": ["Transaction", "Negotiation"]}
              ],
              "summary": {
                "totalEmails": 2,
                "transactionCount": 1,
                "marketingCount": 1,
                "feeRelatedCount": 0,
                "complianceCount": 0,
                "leadNurtureCount": 0,
                "languageDistribution": {"en": 2},
                "dominantTone": "professional",
                "averageEmailLength": "medium"
              }
            }
            """,
            InputTokens: 150, OutputTokens: 80, DurationMs: 900);

    // ---------------------------------------------------------------------------
    // BuildCompactEmailList — JSON format with subject/preview/id
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildCompactEmailList_SingleEmail_ContainsIdSubjectAndPreview()
    {
        var email = MakeEmail(id: "abc-123", subject: "Commission Rate", body: "Let's discuss the commission.");

        var result = EmailClassificationWorker.BuildCompactEmailList(new List<EmailMessage> { email });

        result.Should().Contain("\"id\": \"abc-123\"");
        result.Should().Contain("\"subject\":");
        result.Should().Contain("Commission Rate");
        result.Should().Contain("\"preview\":");
        result.Should().Contain("Let\\u0027s discuss the commission.");
    }

    [Fact]
    public void BuildCompactEmailList_MultipleEmails_AllItemsIncluded()
    {
        var emails = new List<EmailMessage>
        {
            MakeEmail(id: "e1", subject: "First"),
            MakeEmail(id: "e2", subject: "Second"),
            MakeEmail(id: "e3", subject: "Third")
        };

        var result = EmailClassificationWorker.BuildCompactEmailList(emails);

        result.Should().Contain("\"id\": \"e1\"");
        result.Should().Contain("\"id\": \"e2\"");
        result.Should().Contain("\"id\": \"e3\"");
    }

    [Fact]
    public void BuildCompactEmailList_StartsAndEndsWithBrackets()
    {
        var result = EmailClassificationWorker.BuildCompactEmailList(
            new List<EmailMessage> { MakeEmail() });

        result.Trim().Should().StartWith("[");
        result.Trim().Should().EndWith("]");
    }

    // ---------------------------------------------------------------------------
    // BuildCompactEmailList — preview truncation at 200 chars
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildCompactEmailList_LongBody_TruncatesPreviewAt200Chars()
    {
        var longBody = new string('A', 300);
        var email = MakeEmail(body: longBody);

        var result = EmailClassificationWorker.BuildCompactEmailList(new List<EmailMessage> { email });

        // The preview should be exactly 200 A chars, not 300
        var previewStart = result.IndexOf("\"preview\": \"", StringComparison.Ordinal) + "\"preview\": \"".Length;
        var previewEnd = result.IndexOf('"', previewStart);
        var previewContent = result[previewStart..previewEnd];

        previewContent.Length.Should().Be(200);
        previewContent.Should().Be(new string('A', 200));
    }

    [Fact]
    public void BuildCompactEmailList_ShortBody_PreviewIsFullBody()
    {
        var shortBody = "Short message.";
        var email = MakeEmail(body: shortBody);

        var result = EmailClassificationWorker.BuildCompactEmailList(new List<EmailMessage> { email });

        result.Should().Contain("Short message.");
    }

    // ---------------------------------------------------------------------------
    // ParseClassificationResponse — valid JSON with classifications + summary
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseClassificationResponse_ValidJson_ParsesClassificationsAndSummary()
    {
        var json = """
            {
              "classifications": [
                {"id": "msg-001", "categories": ["Transaction", "FeeRelated"]},
                {"id": "msg-002", "categories": ["Marketing"]}
              ],
              "summary": {
                "totalEmails": 50,
                "transactionCount": 10,
                "marketingCount": 15,
                "feeRelatedCount": 5,
                "complianceCount": 2,
                "leadNurtureCount": 8,
                "languageDistribution": {"en": 45, "es": 5},
                "dominantTone": "warm-professional",
                "averageEmailLength": "long"
              }
            }
            """;

        var result = EmailClassificationWorker.ParseClassificationResponse(json);

        result.Classifications.Should().HaveCount(2);
        result.Classifications[0].EmailId.Should().Be("msg-001");
        result.Classifications[0].Categories.Should().Contain(EmailCategory.Transaction);
        result.Classifications[0].Categories.Should().Contain(EmailCategory.FeeRelated);
        result.Classifications[1].EmailId.Should().Be("msg-002");
        result.Classifications[1].Categories.Should().Contain(EmailCategory.Marketing);

        result.Summary.TotalEmails.Should().Be(50);
        result.Summary.TransactionCount.Should().Be(10);
        result.Summary.MarketingCount.Should().Be(15);
        result.Summary.FeeRelatedCount.Should().Be(5);
        result.Summary.ComplianceCount.Should().Be(2);
        result.Summary.LeadNurtureCount.Should().Be(8);
        result.Summary.LanguageDistribution.Should().ContainKey("en").WhoseValue.Should().Be(45);
        result.Summary.LanguageDistribution.Should().ContainKey("es").WhoseValue.Should().Be(5);
        result.Summary.DominantTone.Should().Be("warm-professional");
        result.Summary.AverageEmailLength.Should().Be("long");
    }

    // ---------------------------------------------------------------------------
    // ParseClassificationResponse — JSON wrapped in markdown code fences
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseClassificationResponse_JsonInMarkdownFences_StripsFencesAndParses()
    {
        var fencedJson = """
            ```json
            {
              "classifications": [
                {"id": "msg-xyz", "categories": ["Compliance"]}
              ],
              "summary": {
                "totalEmails": 1,
                "transactionCount": 0,
                "marketingCount": 0,
                "feeRelatedCount": 0,
                "complianceCount": 1,
                "leadNurtureCount": 0,
                "languageDistribution": {"en": 1},
                "dominantTone": "formal",
                "averageEmailLength": "short"
              }
            }
            ```
            """;

        var result = EmailClassificationWorker.ParseClassificationResponse(fencedJson);

        result.Classifications.Should().HaveCount(1);
        result.Classifications[0].EmailId.Should().Be("msg-xyz");
        result.Classifications[0].Categories.Should().Contain(EmailCategory.Compliance);
    }

    [Fact]
    public void ParseClassificationResponse_JsonInPlainCodeFences_StripsFencesAndParses()
    {
        var fencedJson = """
            ```
            {
              "classifications": [],
              "summary": {
                "totalEmails": 0,
                "transactionCount": 0,
                "marketingCount": 0,
                "feeRelatedCount": 0,
                "complianceCount": 0,
                "leadNurtureCount": 0,
                "languageDistribution": {},
                "dominantTone": null,
                "averageEmailLength": null
              }
            }
            ```
            """;

        var result = EmailClassificationWorker.ParseClassificationResponse(fencedJson);

        result.Classifications.Should().BeEmpty();
    }

    // ---------------------------------------------------------------------------
    // ParseClassificationResponse — empty classifications array
    // ---------------------------------------------------------------------------

    [Fact]
    public void ParseClassificationResponse_EmptyClassificationsArray_ReturnsEmptyList()
    {
        var json = """
            {
              "classifications": [],
              "summary": {
                "totalEmails": 0,
                "transactionCount": 0,
                "marketingCount": 0,
                "feeRelatedCount": 0,
                "complianceCount": 0,
                "leadNurtureCount": 0,
                "languageDistribution": {}
              }
            }
            """;

        var result = EmailClassificationWorker.ParseClassificationResponse(json);

        result.Classifications.Should().BeEmpty();
        result.Summary.TotalEmails.Should().Be(0);
    }

    // ---------------------------------------------------------------------------
    // ClassifyAsync — empty corpus returns empty result (no Claude call)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClassifyAsync_EmptyCorpus_ReturnsEmptyResultWithoutCallingClaude()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<EmailClassificationWorker>>();

        var worker = new EmailClassificationWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var emptyCorpus = MakeCorpus(sent: new List<EmailMessage>(), inbox: new List<EmailMessage>());

        var result = await worker.ClassifyAsync(emptyCorpus, CancellationToken.None);

        result.Classifications.Should().BeEmpty();
        result.Summary.TotalEmails.Should().Be(0);
        anthropic.Verify(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------------------
    // ClassifyAsync — combines sent and inbox emails
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClassifyAsync_WithSentAndInboxEmails_CallsClaudeWithCombinedList()
    {
        var anthropic = new Mock<IAnthropicClient>();
        var sanitizer = new Mock<IContentSanitizer>();
        var logger = new Mock<ILogger<EmailClassificationWorker>>();

        string? capturedUserMessage = null;
        sanitizer.Setup(s => s.Sanitize(It.IsAny<string>())).Returns<string>(s => s);
        anthropic.Setup(a => a.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, int, string, CancellationToken>(
                (_, _, userMsg, _, _, _) => capturedUserMessage = userMsg)
            .ReturnsAsync(MakeValidClassificationResponse());

        var worker = new EmailClassificationWorker(anthropic.Object, sanitizer.Object, logger.Object);
        var corpus = MakeCorpus(
            sent: new List<EmailMessage> { MakeEmail(id: "sent-1", subject: "Sent email") },
            inbox: new List<EmailMessage> { MakeEmail(id: "inbox-1", subject: "Inbox email") });

        await worker.ClassifyAsync(corpus, CancellationToken.None);

        anthropic.Verify(a => a.SendAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedUserMessage.Should().Contain("sent-1");
        capturedUserMessage.Should().Contain("inbox-1");
    }
}
