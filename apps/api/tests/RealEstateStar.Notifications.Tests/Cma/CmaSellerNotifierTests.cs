using Microsoft.Extensions.Logging;

namespace RealEstateStar.Notifications.Tests.Cma;

public class CmaSellerNotifierTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private string? _tempPdfPath;

    public CmaSellerNotifierTests()
    {
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ─── Shared test data ──────────────────────────────────────────────────────

    private string CreateTempPdf(string content = "fake pdf bytes")
    {
        _tempPdfPath = Path.Combine(_tempDir, "cma-report.pdf");
        File.WriteAllText(_tempPdfPath, content);
        return _tempPdfPath;
    }

    private static Lead MakeLead() => new()
    {
        Id = new Guid("bbbbbbbb-0000-0000-0000-000000000001"),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Seller,
        FirstName = "Alice",
        LastName = "Johnson",
        Email = "alice@example.com",
        Phone = "5551112222",
        Timeline = "ASAP",
        ReceivedAt = new DateTime(2026, 3, 20, 10, 0, 0, DateTimeKind.Utc),
        Status = LeadStatus.Complete,
        SellerDetails = new SellerDetails
        {
            Address = "456 Oak Ave",
            City = "Montclair",
            State = "NJ",
            Zip = "07042"
        }
    };

    private static CmaAnalysis MakeAnalysis() => new()
    {
        ValueLow = 450_000m,
        ValueMid = 475_000m,
        ValueHigh = 500_000m,
        MarketNarrative = "Strong seller's market with low inventory.",
        MarketTrend = "Appreciating",
        MedianDaysOnMarket = 14
    };

    private static AccountConfig MakeConfig(string? rawAccountId = null) => new()
    {
        Handle = "jenise-buckalew",
        RawAccountId = rawAccountId,
        Agent = new AccountAgent
        {
            Name = "Jenise Buckalew",
            Email = "jenise@example.com",
            Phone = "(973) 555-0100"
        }
    };

    private (CmaSellerNotifier sut, Mock<IGmailSender> gmailSender, Mock<IFileStorageProvider> fanOutStorage)
        BuildSut(Mock<IAccountConfigService>? configService = null, ILogger<CmaSellerNotifier>? logger = null)
    {
        var gmailSender = new Mock<IGmailSender>();
        var fanOutStorage = new Mock<IFileStorageProvider>();
        configService ??= new Mock<IAccountConfigService>();
        var sut = new CmaSellerNotifier(
            gmailSender.Object,
            fanOutStorage.Object,
            configService.Object,
            logger ?? new Mock<ILogger<CmaSellerNotifier>>().Object);
        return (sut, gmailSender, fanOutStorage);
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifySellerAsync_SendsEmailWithPdfAttachment()
    {
        var pdfPath = CreateTempPdf();
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var (sut, gmailSender, _) = BuildSut(configService);

        await sut.NotifySellerAsync("jenise-buckalew", MakeLead(), pdfPath, MakeAnalysis(), "corr-001", CancellationToken.None);

        gmailSender.Verify(g => g.SendWithAttachmentAsync(
            It.IsAny<string>(),
            "jenise-buckalew",
            "alice@example.com",
            It.Is<string>(s => s.Contains("Comparative Market Analysis")),
            It.IsAny<string>(),
            It.Is<byte[]>(b => b.Length > 0),
            "cma-report.pdf",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifySellerAsync_UsesAccountIdFromConfig()
    {
        var pdfPath = CreateTempPdf();
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(rawAccountId: "acct-xyz-789"));

        var (sut, gmailSender, _) = BuildSut(configService);

        await sut.NotifySellerAsync("jenise-buckalew", MakeLead(), pdfPath, MakeAnalysis(), "corr-001", CancellationToken.None);

        gmailSender.Verify(g => g.SendWithAttachmentAsync(
            "acct-xyz-789",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifySellerAsync_WritesEmailRecordToFanOutStorage()
    {
        var pdfPath = CreateTempPdf();
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var (sut, _, fanOutStorage) = BuildSut(configService);

        await sut.NotifySellerAsync("jenise-buckalew", MakeLead(), pdfPath, MakeAnalysis(), "corr-001", CancellationToken.None);

        fanOutStorage.Verify(s => s.WriteDocumentAsync(
            It.Is<string>(f => f.Contains("456 Oak Ave")),
            It.Is<string>(n => n.EndsWith(".md")),
            It.Is<string>(c => c.Contains("leadId")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifySellerAsync_WhenEmailFails_Throws_AndDoesNotWriteRecord()
    {
        var pdfPath = CreateTempPdf();
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var (sut, gmailSender, fanOutStorage) = BuildSut(configService);
        gmailSender.Setup(g => g.SendWithAttachmentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gmail api error"));

        var act = async () => await sut.NotifySellerAsync("jenise-buckalew", MakeLead(), pdfPath, MakeAnalysis(), "corr-001", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("gmail api error");
        fanOutStorage.Verify(s => s.WriteDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifySellerAsync_WhenStorageFailsAfterEmail_LogsErrorAndDoesNotThrow()
    {
        var pdfPath = CreateTempPdf();
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var logger = new Mock<ILogger<CmaSellerNotifier>>();
        var (sut, _, fanOutStorage) = BuildSut(configService, logger.Object);
        fanOutStorage.Setup(s => s.WriteDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("drive unavailable"));

        // Storage failure is non-fatal
        var act = async () => await sut.NotifySellerAsync("jenise-buckalew", MakeLead(), pdfPath, MakeAnalysis(), "corr-001", CancellationToken.None);
        await act.Should().NotThrowAsync();

        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("CMA-NOTIFY-009")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    // ─── BuildEmailBody static helper tests ───────────────────────────────────

    [Fact]
    public void BuildEmailBody_ContainsAddressAndValueRange()
    {
        var body = CmaSellerNotifier.BuildEmailBody(MakeLead(), MakeAnalysis(), "Jenise Buckalew", "(973) 555-0100");

        body.Should().Contain("456 Oak Ave");
        body.Should().Contain("450,000");
        body.Should().Contain("475,000");
        body.Should().Contain("500,000");
    }

    [Fact]
    public void BuildEmailBody_ContainsAgentNameAndPhone()
    {
        var body = CmaSellerNotifier.BuildEmailBody(MakeLead(), MakeAnalysis(), "Jenise Buckalew", "(973) 555-0100");

        body.Should().Contain("Jenise Buckalew");
        body.Should().Contain("(973) 555-0100");
    }

    [Fact]
    public void BuildEmailBody_ContainsMarketTrendAndDaysOnMarket()
    {
        var body = CmaSellerNotifier.BuildEmailBody(MakeLead(), MakeAnalysis(), "Jenise Buckalew", "(973) 555-0100");

        body.Should().Contain("Appreciating");
        body.Should().Contain("14");
    }

    // ─── BuildEmailRecord static helper tests ─────────────────────────────────

    [Fact]
    public void BuildEmailRecord_ContainsFrontmatterFields()
    {
        var record = CmaSellerNotifier.BuildEmailRecord(MakeLead(), "Subject", "Body text", "corr-001");

        record.Should().Contain("leadId:");
        record.Should().Contain("sentAt:");
        record.Should().Contain("subject:");
        record.Should().Contain("recipientEmailHash:");
        record.Should().Contain("correlationId: corr-001");
    }

    [Fact]
    public void BuildEmailRecord_ContainsBodyAfterFrontmatter()
    {
        var record = CmaSellerNotifier.BuildEmailRecord(MakeLead(), "Subject", "Body text here", "corr-001");

        var parts = record.Split("---", StringSplitOptions.None);
        parts.Should().HaveCountGreaterThan(2);
        record.Should().Contain("Body text here");
    }
}
