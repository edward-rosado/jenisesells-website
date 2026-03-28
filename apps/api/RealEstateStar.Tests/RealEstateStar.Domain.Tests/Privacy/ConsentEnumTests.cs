using System.Text.Json;
using RealEstateStar.Domain.Privacy;

namespace RealEstateStar.Domain.Tests.Privacy;

public class ConsentEnumTests
{
    [Theory]
    [InlineData(ConsentAction.OptIn, "\"OptIn\"")]
    [InlineData(ConsentAction.OptOut, "\"OptOut\"")]
    [InlineData(ConsentAction.Resubscribe, "\"Resubscribe\"")]
    [InlineData(ConsentAction.DataAccessRequest, "\"DataAccessRequest\"")]
    [InlineData(ConsentAction.DataExportRequest, "\"DataExportRequest\"")]
    public void ConsentAction_SerializesAsString(ConsentAction action, string expected)
    {
        var json = JsonSerializer.Serialize(action);
        Assert.Equal(expected, json);
    }

    [Theory]
    [InlineData(ConsentSource.LeadForm, "\"LeadForm\"")]
    [InlineData(ConsentSource.PrivacyPage, "\"PrivacyPage\"")]
    [InlineData(ConsentSource.EmailLink, "\"EmailLink\"")]
    [InlineData(ConsentSource.Api, "\"Api\"")]
    public void ConsentSource_SerializesAsString(ConsentSource source, string expected)
    {
        var json = JsonSerializer.Serialize(source);
        Assert.Equal(expected, json);
    }

    [Fact]
    public void ConsentAction_RoundTrips()
    {
        var original = ConsentAction.Resubscribe;
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ConsentAction>(json);
        Assert.Equal(original, deserialized);
    }

    [Fact]
    public void ConsentSource_RoundTrips()
    {
        var original = ConsentSource.LeadForm;
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ConsentSource>(json);
        Assert.Equal(original, deserialized);
    }
}
