using FluentAssertions;
using RealEstateStar.Domain.Shared;

namespace RealEstateStar.Domain.Tests.Shared;

public sealed class NullIdempotencyStoreTests
{
    private readonly NullIdempotencyStore _sut = new();
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task HasCompletedAsync_AlwaysReturnsFalse()
    {
        var result = await _sut.HasCompletedAsync("lead:agent1-lead1:email-send", Ct);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasCompletedAsync_ReturnsFalseEvenAfterMarkCompleted()
    {
        await _sut.MarkCompletedAsync("lead:agent1-lead1:email-send", Ct);
        var result = await _sut.HasCompletedAsync("lead:agent1-lead1:email-send", Ct);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkCompletedAsync_DoesNotThrow()
    {
        var act = async () => await _sut.MarkCompletedAsync("lead:agent1-lead1:email-send", Ct);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MarkCompletedAsync_IsIdempotent_DoesNotThrowOnMultipleCalls()
    {
        var act = async () =>
        {
            await _sut.MarkCompletedAsync("lead:agent1-lead1:email-send", Ct);
            await _sut.MarkCompletedAsync("lead:agent1-lead1:email-send", Ct);
        };
        await act.Should().NotThrowAsync();
    }
}
