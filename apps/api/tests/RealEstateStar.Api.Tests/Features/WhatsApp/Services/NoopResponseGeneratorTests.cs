using FluentAssertions;
using RealEstateStar.Api.Features.WhatsApp.Services;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Services;

public class NoopResponseGeneratorTests
{
    private readonly NoopResponseGenerator _sut = new();

    [Fact]
    public async Task GenerateAsync_ReturnsNonEmptyPlaceholder()
    {
        var result = await _sut.GenerateAsync("Alice", "Tell me about Jane", "Jane Smith", CancellationToken.None);

        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateAsync_NullLeadName_ReturnsPlaceholder()
    {
        var result = await _sut.GenerateAsync("Bob", "Any questions?", null, CancellationToken.None);

        result.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateAsync_HonoursCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Noop is synchronous internally — cancellation does not throw.
        var act = async () => await _sut.GenerateAsync("Alice", "msg", null, cts.Token);
        await act.Should().NotThrowAsync();
    }
}
