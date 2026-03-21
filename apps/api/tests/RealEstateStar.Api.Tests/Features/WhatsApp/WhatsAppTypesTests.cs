using System.Text.Json;
using FluentAssertions;
using RealEstateStar.Api.Features.WhatsApp;

namespace RealEstateStar.Api.Tests.Features.WhatsApp;

public class WhatsAppTypesTests
{
    [Fact]
    public void NotificationType_SerializesAsString()
    {
        var json = JsonSerializer.Serialize(NotificationType.NewLead);
        json.Should().Contain("NewLead");
    }

    [Fact]
    public void IntentType_SerializesAsString()
    {
        var json = JsonSerializer.Serialize(IntentType.LeadQuestion);
        json.Should().Contain("LeadQuestion");
    }

    [Fact]
    public void OutOfScopeCategory_SerializesAsString()
    {
        var json = JsonSerializer.Serialize(OutOfScopeCategory.PromptInjection);
        json.Should().Contain("PromptInjection");
    }

    [Fact]
    public void NotificationType_DeserializesFromString()
    {
        var result = JsonSerializer.Deserialize<NotificationType>("\"CmaReady\"");
        result.Should().Be(NotificationType.CmaReady);
    }
}
