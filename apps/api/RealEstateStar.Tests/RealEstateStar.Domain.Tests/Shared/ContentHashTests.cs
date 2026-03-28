using FluentAssertions;
using RealEstateStar.Domain.Shared;

namespace RealEstateStar.Domain.Tests.Shared;

public sealed class ContentHashTests
{
    [Fact]
    public void Compute_SameInputs_ReturnsSameHash()
    {
        var hash1 = ContentHash.Compute("123 Main St", "Newark", "NJ", "07101");
        var hash2 = ContentHash.Compute("123 Main St", "Newark", "NJ", "07101");

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Compute_DifferentInputs_ReturnsDifferentHash()
    {
        var hash1 = ContentHash.Compute("123 Main St", "Newark", "NJ", "07101");
        var hash2 = ContentHash.Compute("456 Oak Ave", "Newark", "NJ", "07101");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Compute_NullFields_HandledGracefully()
    {
        var act = () => ContentHash.Compute("123 Main St", null, "NJ", null);

        act.Should().NotThrow();
    }

    [Fact]
    public void Compute_NullTreatedAsEmpty_ConsistentWithEmptyString()
    {
        var hashWithNull = ContentHash.Compute("a", null, "c");
        var hashWithEmpty = ContentHash.Compute("a", "", "c");

        hashWithNull.Should().Be(hashWithEmpty);
    }

    [Fact]
    public void Compute_Deterministic_AcrossMultipleCalls()
    {
        const string a = "city";
        const string b = "state";
        const string c = "budget";

        var hashes = Enumerable.Range(0, 10)
            .Select(_ => ContentHash.Compute(a, b, c))
            .ToList();

        hashes.Distinct().Should().HaveCount(1);
    }

    [Fact]
    public void Compute_OrderMatters_DifferentOrderDifferentHash()
    {
        var hash1 = ContentHash.Compute("Newark", "NJ");
        var hash2 = ContentHash.Compute("NJ", "Newark");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Compute_ReturnsLowercaseHexString()
    {
        var hash = ContentHash.Compute("test");

        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Compute_AllNulls_ReturnsConsistentHash()
    {
        var hash1 = ContentHash.Compute(null, null, null);
        var hash2 = ContentHash.Compute(null, null, null);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Compute_SingleField_WorksCorrectly()
    {
        var hash1 = ContentHash.Compute("only-field");
        var hash2 = ContentHash.Compute("only-field");

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
    }

    [Fact]
    public void Compute_EmptyParams_ReturnsConsistentHash()
    {
        var hash1 = ContentHash.Compute();
        var hash2 = ContentHash.Compute();

        hash1.Should().Be(hash2);
    }
}
