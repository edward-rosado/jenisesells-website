using FluentAssertions;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Tests.Activation.Models;

public class EmailAttachmentTests
{
    [Fact]
    public void EmailAttachment_Roundtrips()
    {
        var attachment = new EmailAttachment("att-1", "photo.jpg", "image/jpeg", 1024);
        attachment.AttachmentId.Should().Be("att-1");
        attachment.Filename.Should().Be("photo.jpg");
        attachment.MimeType.Should().Be("image/jpeg");
        attachment.SizeBytes.Should().Be(1024);
    }

    [Fact]
    public void EmailMessage_IncludesAttachments()
    {
        var attachments = new List<EmailAttachment>
        {
            new("att-1", "doc.pdf", "application/pdf", 2048),
            new("att-2", "photo.jpg", "image/jpeg", 4096),
        };
        var message = new EmailMessage(
            "msg-1", "Test Subject", "Body text", "from@test.com",
            ["to@test.com"], DateTime.UtcNow, null, attachments);
        message.Attachments.Should().HaveCount(2);
        message.Attachments[0].Filename.Should().Be("doc.pdf");
        message.Attachments[1].SizeBytes.Should().Be(4096);
    }

    [Fact]
    public void EmailMessage_DefaultsToEmptyAttachments()
    {
        var message = new EmailMessage(
            "msg-1", "Subject", "Body", "from@test.com",
            ["to@test.com"], DateTime.UtcNow, null, []);
        message.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void AgentDiscovery_IncludesLanguages()
    {
        var discovery = new AgentDiscovery(
            HeadshotBytes: null, LogoBytes: null, Phone: null,
            Websites: [], Reviews: [], Profiles: [],
            Ga4MeasurementId: null, WhatsAppEnabled: false,
            Languages: ["English", "Spanish"]);
        discovery.Languages.Should().HaveCount(2);
        discovery.Languages.Should().Contain("English");
        discovery.Languages.Should().Contain("Spanish");
    }
}
