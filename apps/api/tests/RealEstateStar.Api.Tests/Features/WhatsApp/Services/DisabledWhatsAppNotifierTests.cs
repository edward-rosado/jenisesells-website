using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.DataServices.WhatsApp;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Services;

public class DisabledWhatsAppNotifierTests
{
    private readonly DisabledWhatsAppNotifier _sut =
        new(NullLogger<DisabledWhatsAppNotifier>.Instance);

    [Fact]
    public async Task NotifyAsync_DoesNotThrow_ForAnyNotificationType()
    {
        foreach (var type in Enum.GetValues<NotificationType>())
        {
            var act = async () =>
                await _sut.NotifyAsync("agent-1", type, "Jane", [], CancellationToken.None);
            await act.Should().NotThrowAsync(because: $"{type} should be silently ignored");
        }
    }

    [Fact]
    public async Task NotifyAsync_NullLeadName_DoesNotThrow()
    {
        var act = async () =>
            await _sut.NotifyAsync("agent-1", NotificationType.NewLead, null, [], CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void RecordAgentMessage_DoesNotThrow()
    {
        var act = () => _sut.RecordAgentMessage("+15551234567");
        act.Should().NotThrow();
    }

    [Fact]
    public void IsWindowOpen_AlwaysReturnsFalse()
    {
        _sut.IsWindowOpen("+15551234567").Should().BeFalse();
    }
}
