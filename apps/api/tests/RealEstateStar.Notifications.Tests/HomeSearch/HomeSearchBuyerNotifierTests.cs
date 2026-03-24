using Microsoft.Extensions.Logging;

namespace RealEstateStar.Notifications.Tests.HomeSearch;

public class HomeSearchBuyerNotifierTests
{
    // ─── Shared test data ──────────────────────────────────────────────────────

    private static Lead MakeLead() => new()
    {
        Id = new Guid("cccccccc-0000-0000-0000-000000000001"),
        AgentId = "jenise-buckalew",
        LeadType = LeadType.Buyer,
        FirstName = "Bob",
        LastName = "Smith",
        Email = "bob@example.com",
        Phone = "5559998888",
        Timeline = "1-3months",
        ReceivedAt = new DateTime(2026, 3, 21, 9, 0, 0, DateTimeKind.Utc),
        Status = LeadStatus.Complete,
        BuyerDetails = new BuyerDetails
        {
            City = "Montclair",
            State = "NJ",
            MaxBudget = 600_000m,
            Bedrooms = 3
        }
    };

    private static List<Listing> MakeListings() =>
    [
        new Listing("123 Main St", "Montclair", "NJ", "07042", 550_000m, 3, 2m, 1800, "Great match!", "https://zillow.com/1"),
        new Listing("456 Oak Ave", "Montclair", "NJ", "07042", 580_000m, 3, 2.5m, 2000, null, null)
    ];

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

    private (HomeSearchBuyerNotifier sut, Mock<IGmailSender> gmailSender, Mock<IDocumentStorageProvider> fanOutStorage)
        BuildSut(Mock<IAccountConfigService>? configService = null, ILogger<HomeSearchBuyerNotifier>? logger = null)
    {
        var gmailSender = new Mock<IGmailSender>();
        var fanOutStorage = new Mock<IDocumentStorageProvider>();
        configService ??= new Mock<IAccountConfigService>();
        var sut = new HomeSearchBuyerNotifier(
            gmailSender.Object,
            fanOutStorage.Object,
            configService.Object,
            logger ?? new Mock<ILogger<HomeSearchBuyerNotifier>>().Object);
        return (sut, gmailSender, fanOutStorage);
    }

    // ─── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotifyBuyerAsync_SendsEmailViaSgmailSender()
    {
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var (sut, gmailSender, _) = BuildSut(configService);

        await sut.NotifyBuyerAsync("jenise-buckalew", MakeLead(), MakeListings(), "corr-001", CancellationToken.None);

        gmailSender.Verify(g => g.SendAsync(
            It.IsAny<string>(),
            "jenise-buckalew",
            "bob@example.com",
            It.Is<string>(s => s.Contains("Home Search Results")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyBuyerAsync_UsesAccountIdFromConfig()
    {
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig(rawAccountId: "acct-buyer-456"));

        var (sut, gmailSender, _) = BuildSut(configService);

        await sut.NotifyBuyerAsync("jenise-buckalew", MakeLead(), MakeListings(), "corr-001", CancellationToken.None);

        gmailSender.Verify(g => g.SendAsync(
            "acct-buyer-456",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyBuyerAsync_WritesListingsToFanOutStorage()
    {
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var (sut, _, fanOutStorage) = BuildSut(configService);

        await sut.NotifyBuyerAsync("jenise-buckalew", MakeLead(), MakeListings(), "corr-001", CancellationToken.None);

        fanOutStorage.Verify(s => s.WriteDocumentAsync(
            It.Is<string>(f => f.Contains("Home Search")),
            It.Is<string>(n => n.EndsWith(".md") && n.Contains("Home Search Results")),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotifyBuyerAsync_WhenEmailFails_Throws_AndDoesNotWriteStorage()
    {
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var (sut, gmailSender, fanOutStorage) = BuildSut(configService);
        gmailSender.Setup(g => g.SendAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("gmail error"));

        var act = async () => await sut.NotifyBuyerAsync("jenise-buckalew", MakeLead(), MakeListings(), "corr-001", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("gmail error");
        fanOutStorage.Verify(s => s.WriteDocumentAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyBuyerAsync_WhenStorageFailsAfterEmail_LogsErrorAndDoesNotThrow()
    {
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeConfig());

        var logger = new Mock<ILogger<HomeSearchBuyerNotifier>>();
        var (sut, _, fanOutStorage) = BuildSut(configService, logger.Object);
        fanOutStorage.Setup(s => s.WriteDocumentAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("storage unavailable"));

        // Storage failure is non-fatal
        var act = async () => await sut.NotifyBuyerAsync("jenise-buckalew", MakeLead(), MakeListings(), "corr-001", CancellationToken.None);
        await act.Should().NotThrowAsync();

        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("HS-NOTIFY-006")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task NotifyBuyerAsync_FallsBackToAgentIdAsAccountId_WhenConfigNull()
    {
        var configService = new Mock<IAccountConfigService>();
        configService.Setup(c => c.GetAccountAsync("jenise-buckalew", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccountConfig?)null);

        var (sut, gmailSender, _) = BuildSut(configService);

        await sut.NotifyBuyerAsync("jenise-buckalew", MakeLead(), MakeListings(), "corr-001", CancellationToken.None);

        gmailSender.Verify(g => g.SendAsync(
            "jenise-buckalew",  // falls back to agentId when config is null
            "jenise-buckalew",
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
