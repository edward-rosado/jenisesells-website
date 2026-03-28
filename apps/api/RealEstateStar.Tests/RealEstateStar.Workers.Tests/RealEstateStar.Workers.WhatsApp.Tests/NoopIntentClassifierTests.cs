using FluentAssertions;
using RealEstateStar.Domain.WhatsApp.Models;
using RealEstateStar.Workers.WhatsApp;

namespace RealEstateStar.Workers.WhatsApp.Tests;

public class NoopIntentClassifierTests
{
    private readonly NoopIntentClassifier _sut = new();

    [Fact]
    public async Task ClassifyAsync_AlwaysReturnsInScopeLeadQuestion()
    {
        var result = await _sut.ClassifyAsync("tell me about Jane Smith", CancellationToken.None);

        result.InScope.Should().BeTrue();
        result.Intent.Should().Be(IntentType.LeadQuestion);
    }

    [Fact]
    public async Task ClassifyAsync_EmptyMessage_StillReturnsInScope()
    {
        var result = await _sut.ClassifyAsync("", CancellationToken.None);

        result.InScope.Should().BeTrue();
    }

    [Fact]
    public async Task ClassifyAsync_OutOfScopeTopic_StillReturnsInScope()
    {
        // The noop never classifies out-of-scope — every message routes to the response generator.
        var result = await _sut.ClassifyAsync("what is the stock price?", CancellationToken.None);

        result.InScope.Should().BeTrue();
        result.Intent.Should().Be(IntentType.LeadQuestion);
    }

    [Fact]
    public async Task ClassifyAsync_HonoursCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Noop is synchronous internally, so it does not throw even if already cancelled.
        var act = async () => await _sut.ClassifyAsync("message", cts.Token);
        await act.Should().NotThrowAsync();
    }
}
