using RealEstateStar.Api.Features.WhatsApp;

namespace RealEstateStar.Api.Tests.Features.WhatsApp;

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
