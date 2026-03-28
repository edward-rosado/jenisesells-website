using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Markdown;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Markdown;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Cma.Services;
using RealEstateStar.Domain.HomeSearch.Markdown;
using RealEstateStar.Domain.WhatsApp.Models;
using RealEstateStar.Domain.WhatsApp;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Services;
using RealEstateStar.Domain.Onboarding;

namespace RealEstateStar.Domain.Tests.WhatsApp;

public class WhatsAppDiagnosticsTests
{
    [Fact]
    public void ActivitySource_HasCorrectName()
    {
        Assert.Equal("RealEstateStar.WhatsApp", WhatsAppDiagnostics.ActivitySource.Name);
    }

    [Fact]
    public void Meter_HasCorrectName()
    {
        Assert.Equal("RealEstateStar.WhatsApp", WhatsAppDiagnostics.Meter.Name);
    }

    [Fact]
    public void AllInstruments_AreInitialized()
    {
        Assert.NotNull(WhatsAppDiagnostics.MessagesReceived);
        Assert.NotNull(WhatsAppDiagnostics.MessagesSentTemplate);
        Assert.NotNull(WhatsAppDiagnostics.MessagesSentFreeform);
        Assert.NotNull(WhatsAppDiagnostics.MessagesDuplicate);
        Assert.NotNull(WhatsAppDiagnostics.WebhookSignatureFail);
        Assert.NotNull(WhatsAppDiagnostics.AgentNotFound);
        Assert.NotNull(WhatsAppDiagnostics.IntentClassified);
        Assert.NotNull(WhatsAppDiagnostics.QueueEnqueued);
        Assert.NotNull(WhatsAppDiagnostics.QueueProcessed);
        Assert.NotNull(WhatsAppDiagnostics.QueueFailed);
        Assert.NotNull(WhatsAppDiagnostics.QueuePoison);
        Assert.NotNull(WhatsAppDiagnostics.AuditWritten);
        Assert.NotNull(WhatsAppDiagnostics.AuditFailed);
        Assert.NotNull(WhatsAppDiagnostics.WebhookProcessingMs);
        Assert.NotNull(WhatsAppDiagnostics.QueueProcessingMs);
        Assert.NotNull(WhatsAppDiagnostics.SendLatencyMs);
        Assert.NotNull(WhatsAppDiagnostics.QueueWaitMs);
    }
}
