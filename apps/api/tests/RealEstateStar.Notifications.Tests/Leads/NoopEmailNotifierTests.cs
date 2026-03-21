using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealEstateStar.Notifications.Tests.Leads;

public class NoopEmailNotifierTests
{
    private readonly NoopEmailNotifier _sut =
        new(NullLogger<NoopEmailNotifier>.Instance);

    [Fact]
    public async Task SendLeadNotificationAsync_DoesNotThrow()
    {
        var lead = new LeadNotification("Jane Smith", "+15551234567", "jane@example.com",
            "Buy", "Princeton, NJ");

        var act = async () =>
            await _sut.SendLeadNotificationAsync("agent-1", lead, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendLeadNotificationAsync_EmptyAgentId_DoesNotThrow()
    {
        var lead = new LeadNotification("Bob", "", "", "", "");

        var act = async () =>
            await _sut.SendLeadNotificationAsync("", lead, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
