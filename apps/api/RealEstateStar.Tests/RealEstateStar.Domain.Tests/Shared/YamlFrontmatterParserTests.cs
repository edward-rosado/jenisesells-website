
namespace RealEstateStar.Domain.Tests.Shared;

public class YamlFrontmatterParserTests
{
    // ── Parse ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ExtractsKeyValuePairsFromFrontmatter()
    {
        var content = """
            ---
            name: Jane Doe
            email: jane@example.com
            phone: 555-1234
            ---
            # Body
            """;

        var result = YamlFrontmatterParser.Parse(content);

        Assert.Equal("Jane Doe", result["name"]);
        Assert.Equal("jane@example.com", result["email"]);
        Assert.Equal("555-1234", result["phone"]);
    }

    [Fact]
    public void Parse_ReturnsEmptyDictForContentWithNoFrontmatter()
    {
        var content = """
            # Just a Markdown File

            No frontmatter here.
            """;

        var result = YamlFrontmatterParser.Parse(content);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_HandlesMultiLineFrontmatter()
    {
        var content = """
            ---
            title: My Lead
            status: new
            source: website
            score: 42
            ---

            Body text here.
            """;

        var result = YamlFrontmatterParser.Parse(content);

        Assert.Equal(4, result.Count);
        Assert.Equal("My Lead", result["title"]);
        Assert.Equal("new", result["status"]);
        Assert.Equal("website", result["source"]);
        Assert.Equal("42", result["score"]);
    }

    [Fact]
    public void Parse_SkipsYamlComments()
    {
        var content = """
            ---
            # This is a comment
            name: Jane Doe
            # Another comment
            email: jane@example.com
            ---
            Body
            """;

        var result = YamlFrontmatterParser.Parse(content);

        Assert.Equal(2, result.Count);
        Assert.Equal("Jane Doe", result["name"]);
        Assert.Equal("jane@example.com", result["email"]);
    }

    [Fact]
    public void Parse_HandlesYamlArraysAsString()
    {
        var content = """
            ---
            name: Jane Doe
            tags: [buyer, hot-lead, referral]
            ---
            Body
            """;

        var result = YamlFrontmatterParser.Parse(content);

        Assert.Equal("Jane Doe", result["name"]);
        Assert.Equal("[buyer, hot-lead, referral]", result["tags"]);
    }

    [Fact]
    public void Parse_UnquotesQuotedStringValues()
    {
        var content = """
            ---
            name: "Jane Doe"
            city: 'Springfield'
            note: unquoted value
            ---
            Body
            """;

        var result = YamlFrontmatterParser.Parse(content);

        Assert.Equal("Jane Doe", result["name"]);
        Assert.Equal("Springfield", result["city"]);
        Assert.Equal("unquoted value", result["note"]);
    }

    [Fact]
    public void Parse_HandlesContentThatStartsWithoutFrontmatter()
    {
        var content = "Just some plain text without any frontmatter fences at all.";

        var result = YamlFrontmatterParser.Parse(content);

        Assert.Empty(result);
    }

    // ── UpdateField ────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateField_ReplacesExistingFieldValue()
    {
        var content = """
            ---
            name: Jane Doe
            status: new
            ---
            # Body

            Some content.
            """;

        var result = YamlFrontmatterParser.UpdateField(content, "status", "contacted");

        var parsed = YamlFrontmatterParser.Parse(result);
        Assert.Equal("contacted", parsed["status"]);
        Assert.Equal("Jane Doe", parsed["name"]);
    }

    [Fact]
    public void UpdateField_AddsNewFieldWhenNotPresent()
    {
        var content = """
            ---
            name: Jane Doe
            ---
            # Body
            """;

        var result = YamlFrontmatterParser.UpdateField(content, "status", "new");

        var parsed = YamlFrontmatterParser.Parse(result);
        Assert.Equal("new", parsed["status"]);
        Assert.Equal("Jane Doe", parsed["name"]);
    }

    [Fact]
    public void UpdateField_PreservesMarkdownBodyUnchanged()
    {
        var content = """
            ---
            name: Jane Doe
            ---
            # Body

            Some **markdown** content here.

            - item 1
            - item 2
            """;

        var result = YamlFrontmatterParser.UpdateField(content, "name", "John Smith");

        // Body must be preserved exactly
        Assert.Contains("# Body", result);
        Assert.Contains("Some **markdown** content here.", result);
        Assert.Contains("- item 1", result);
        Assert.Contains("- item 2", result);
    }
}
