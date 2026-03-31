using FluentAssertions;
using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Domain.Tests.Activation.Models;

public class ClassifiedDriveIndexTests
{
    [Theory]
    [InlineData("123 Oak Street, Springfield", "123-oak-street-springfield")]
    [InlineData("456 Elm Ave #2B, New York", "456-elm-ave-2b-new-york")]
    [InlineData("  spaces  everywhere  ", "spaces-everywhere")]
    [InlineData("UPPER CASE DRIVE", "upper-case-drive")]
    [InlineData("special!@#chars$%^here", "specialcharshere")]
    [InlineData("123---multiple---dashes", "123-multiple-dashes")]
    public void ToSlug_GeneratesUrlSafeSlug(string address, string expectedSlug)
    {
        var result = ClassifiedDriveIndex.ToSlug(address);
        result.Should().Be(expectedSlug);
    }

    [Fact]
    public void ComputePropertyGroups_GroupsFilesByAddress()
    {
        var files = new List<ClassifiedFile>
        {
            new("f1", "front.jpg", "image/jpeg", "properties/oak", FileCategory.PropertyPhoto, "123 Oak St", "high"),
            new("f2", "deed.pdf", "application/pdf", "properties/oak", FileCategory.PropertyDocument, "123 Oak St", "high"),
            new("f3", "back.jpg", "image/jpeg", "properties/elm", FileCategory.PropertyPhoto, "456 Elm Ave", "medium"),
            new("f4", "flyer.pdf", "application/pdf", "marketing", FileCategory.Marketing, null, "high"),
        };
        var groups = ClassifiedDriveIndex.ComputePropertyGroups(files);
        groups.Should().HaveCount(2);
        var oakGroup = groups.First(g => g.Address == "123 Oak St");
        oakGroup.Slug.Should().Be("123-oak-st");
        oakGroup.Photos.Should().HaveCount(1);
        oakGroup.Photos[0].Id.Should().Be("f1");
        oakGroup.Documents.Should().HaveCount(1);
        oakGroup.Documents[0].Id.Should().Be("f2");
        var elmGroup = groups.First(g => g.Address == "456 Elm Ave");
        elmGroup.Photos.Should().HaveCount(1);
        elmGroup.Documents.Should().BeEmpty();
    }

    [Fact]
    public void ComputePropertyGroups_ReturnsEmpty_WhenNoPropertyFiles()
    {
        var files = new List<ClassifiedFile>
        {
            new("f1", "flyer.pdf", "application/pdf", "marketing", FileCategory.Marketing, null, "high"),
            new("f2", "logo.png", "image/png", "branding", FileCategory.Branding, null, "medium"),
        };
        var groups = ClassifiedDriveIndex.ComputePropertyGroups(files);
        groups.Should().BeEmpty();
    }

    [Fact]
    public void ComputePropertyGroups_IgnoresFilesWithNullAddress()
    {
        var files = new List<ClassifiedFile>
        {
            new("f1", "photo.jpg", "image/jpeg", "photos", FileCategory.PropertyPhoto, null, "low"),
        };
        var groups = ClassifiedDriveIndex.ComputePropertyGroups(files);
        groups.Should().BeEmpty();
    }

    [Fact]
    public void ComputePropertyGroups_SeparatesPhotosAndDocuments()
    {
        var files = new List<ClassifiedFile>
        {
            new("f1", "photo1.jpg", "image/jpeg", "123", FileCategory.PropertyPhoto, "100 Main St", "high"),
            new("f2", "photo2.png", "image/png", "123", FileCategory.PropertyPhoto, "100 Main St", "high"),
            new("f3", "contract.pdf", "application/pdf", "123", FileCategory.PropertyDocument, "100 Main St", "high"),
            new("f4", "disclosure.pdf", "application/pdf", "123", FileCategory.PropertyDocument, "100 Main St", "medium"),
        };
        var groups = ClassifiedDriveIndex.ComputePropertyGroups(files);
        groups.Should().HaveCount(1);
        var group = groups[0];
        group.Photos.Should().HaveCount(2);
        group.Documents.Should().HaveCount(2);
    }

    [Fact]
    public void FileCategory_SerializesToJson()
    {
        var file = new ClassifiedFile("f1", "test.pdf", "application/pdf", "/docs", FileCategory.Cma, null, "high");
        var json = System.Text.Json.JsonSerializer.Serialize(file);
        json.Should().Contain("\"Cma\"");
    }

    [Fact]
    public void FileCategory_DeserializesFromJson()
    {
        var json = """{"Id":"f1","Name":"test.pdf","MimeType":"application/pdf","FolderPath":"/docs","Category":"PropertyPhoto","PropertyAddress":null,"Confidence":"high"}""";
        var file = System.Text.Json.JsonSerializer.Deserialize<ClassifiedFile>(json);
        file.Should().NotBeNull();
        file!.Category.Should().Be(FileCategory.PropertyPhoto);
    }

    [Fact]
    public void ToSlug_ReturnsEmpty_ForEmptyInput()
    {
        var result = ClassifiedDriveIndex.ToSlug("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void ToSlug_CollapsesConsecutiveHyphens()
    {
        var result = ClassifiedDriveIndex.ToSlug("123 - Main - St");
        result.Should().Be("123-main-st");
    }
}
