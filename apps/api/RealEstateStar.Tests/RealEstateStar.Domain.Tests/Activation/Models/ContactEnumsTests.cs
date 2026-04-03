using System.Text.Json;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Tests.Activation.Models;

public class ContactEnumsTests
{
    [Theory]
    [InlineData(DocumentType.ListingAgreement, "\"ListingAgreement\"")]
    [InlineData(DocumentType.ClosingStatement, "\"ClosingStatement\"")]
    [InlineData(DocumentType.Other, "\"Other\"")]
    public void DocumentType_serializes_as_string(DocumentType value, string expected)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.Equal(expected, json);
        var deserialized = JsonSerializer.Deserialize<DocumentType>(json);
        Assert.Equal(value, deserialized);
    }

    [Theory]
    [InlineData(ContactRole.Buyer, "\"Buyer\"")]
    [InlineData(ContactRole.Unknown, "\"Unknown\"")]
    public void ContactRole_serializes_as_string(ContactRole value, string expected)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.Equal(expected, json);
        var deserialized = JsonSerializer.Deserialize<ContactRole>(json);
        Assert.Equal(value, deserialized);
    }

    [Theory]
    [InlineData(PipelineStage.Lead, "\"Lead\"")]
    [InlineData(PipelineStage.Closed, "\"Closed\"")]
    public void PipelineStage_serializes_as_string(PipelineStage value, string expected)
    {
        var json = JsonSerializer.Serialize(value);
        Assert.Equal(expected, json);
        var deserialized = JsonSerializer.Deserialize<PipelineStage>(json);
        Assert.Equal(value, deserialized);
    }
}
