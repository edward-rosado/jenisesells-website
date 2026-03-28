using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Leads.Markdown;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.DataServices.Config;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.DataServices.WhatsApp;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealEstateStar.DataServices.Tests.WhatsApp;

public class DisabledWhatsAppAuditServiceTests
{
    private readonly DisabledWhatsAppAuditService _sut =
        new(NullLogger<DisabledWhatsAppAuditService>.Instance);

    [Fact]
    public async Task RecordReceivedAsync_DoesNotThrow()
    {
        var act = async () => await _sut.RecordReceivedAsync(
            "msg-1", "+15551234567", "phone-id", "Hello", "text", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateProcessingAsync_DoesNotThrow()
    {
        var act = async () => await _sut.UpdateProcessingAsync("msg-1", "agent-1", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateCompletedAsync_DoesNotThrow()
    {
        var act = async () => await _sut.UpdateCompletedAsync(
            "msg-1", "agent-1", "LeadQuestion", "response text", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateFailedAsync_NullAgentId_DoesNotThrow()
    {
        var act = async () => await _sut.UpdateFailedAsync("msg-1", null, "error detail", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdateFailedAsync_WithAgentId_DoesNotThrow()
    {
        var act = async () => await _sut.UpdateFailedAsync("msg-1", "agent-1", "error detail", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task UpdatePoisonAsync_DoesNotThrow()
    {
        var act = async () => await _sut.UpdatePoisonAsync("msg-1", "poison reason", CancellationToken.None);
        await act.Should().NotThrowAsync();
    }
}
