using System.Text.Json;
using FluentAssertions;
using RealEstateStar.DataServices.Config;
using RealEstateStar.Domain.Shared.Models;

namespace RealEstateStar.DataServices.Tests.Config;

/// <summary>
/// Tests for the file-based compare-and-swap (CAS) implementation of SaveIfUnchangedAsync
/// and the ETag population in GetAccountAsync.
/// </summary>
public class AccountConfigServiceConcurrencyTests : IDisposable
{
    private readonly string _tempDir;
    private const string Handle = "test-agent";

    public AccountConfigServiceConcurrencyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"res-cas-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_tempDir, Handle));
        WriteConfig(Handle, BuildConfig(Handle, "Initial Name"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ────────────────────────────────────────────────────────────────
    // ETag population on read
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountAsync_PopulatesETag_AfterRead()
    {
        var svc = CreateService();

        var config = await svc.GetAccountAsync(Handle, CancellationToken.None);

        config.Should().NotBeNull();
        config!.ETag.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAccountAsync_ETag_IsConsistentAcrossReads()
    {
        var svc = CreateService();

        var first = await svc.GetAccountAsync(Handle, CancellationToken.None);
        var second = await svc.GetAccountAsync(Handle, CancellationToken.None);

        first!.ETag.Should().Be(second!.ETag);
    }

    // ────────────────────────────────────────────────────────────────
    // SaveIfUnchangedAsync — happy path
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveIfUnchangedAsync_ReturnsTrueAndPersists_WhenETagMatches()
    {
        var svc = CreateService();
        var original = await svc.GetAccountAsync(Handle, CancellationToken.None);
        var updated = BuildConfig(Handle, "Updated Name");

        var committed = await svc.SaveIfUnchangedAsync(updated, original!.ETag!, CancellationToken.None);

        committed.Should().BeTrue();

        var reread = await svc.GetAccountAsync(Handle, CancellationToken.None);
        reread!.Agent!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task SaveIfUnchangedAsync_ETagChanges_AfterSuccessfulSave()
    {
        var svc = CreateService();
        var original = await svc.GetAccountAsync(Handle, CancellationToken.None);
        var originalEtag = original!.ETag!;
        var updated = BuildConfig(Handle, "Changed");

        await svc.SaveIfUnchangedAsync(updated, originalEtag, CancellationToken.None);

        // Wait a tick to ensure filesystem time changes (some filesystems have 1ms resolution)
        await Task.Delay(10);

        var reread = await svc.GetAccountAsync(Handle, CancellationToken.None);
        reread!.ETag.Should().NotBe(originalEtag,
            because: "a successful write updates last-write-time, so the ETag must differ");
    }

    // ────────────────────────────────────────────────────────────────
    // SaveIfUnchangedAsync — conflict path
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveIfUnchangedAsync_ReturnsFalse_WhenETagIsStale()
    {
        var svc = CreateService();
        var original = await svc.GetAccountAsync(Handle, CancellationToken.None);
        var staleEtag = original!.ETag!;

        // Write directly to advance the file's last-write-time.
        await Task.Delay(10);
        WriteConfig(Handle, BuildConfig(Handle, "Concurrent Write"));

        var committed = await svc.SaveIfUnchangedAsync(
            BuildConfig(Handle, "Should Fail"), staleEtag, CancellationToken.None);

        committed.Should().BeFalse();

        // File should still contain the concurrent write, not our update.
        var reread = await svc.GetAccountAsync(Handle, CancellationToken.None);
        reread!.Agent!.Name.Should().Be("Concurrent Write");
    }

    // ────────────────────────────────────────────────────────────────
    // Concurrent-save scenario
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveIfUnchangedAsync_ConcurrentSaves_OnlyFirstSucceeds()
    {
        var svc = CreateService();

        // Two readers get the same ETag.
        var readerA = await svc.GetAccountAsync(Handle, CancellationToken.None);
        var readerB = await svc.GetAccountAsync(Handle, CancellationToken.None);

        readerA!.ETag.Should().Be(readerB!.ETag);
        var sharedEtag = readerA.ETag!;

        // First writer wins.
        var committedA = await svc.SaveIfUnchangedAsync(
            BuildConfig(Handle, "Writer A"), sharedEtag, CancellationToken.None);
        committedA.Should().BeTrue();

        // Second writer sees stale ETag.
        var committedB = await svc.SaveIfUnchangedAsync(
            BuildConfig(Handle, "Writer B"), sharedEtag, CancellationToken.None);
        committedB.Should().BeFalse();

        // File should contain Writer A's changes.
        var final = await svc.GetAccountAsync(Handle, CancellationToken.None);
        final!.Agent!.Name.Should().Be("Writer A");
    }

    // ────────────────────────────────────────────────────────────────
    // Null / missing ETag guard
    // ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveIfUnchangedAsync_WithNullEtag_ReturnsFalse()
    {
        var svc = CreateService();
        var config = BuildConfig(Handle, "No ETag");

        // ETag is null — should be treated as a mismatch (not a blind write).
        var committed = await svc.SaveIfUnchangedAsync(config, null!, CancellationToken.None);

        committed.Should().BeFalse();
    }

    // ────────────────────────────────────────────────────────────────
    // Helpers
    // ────────────────────────────────────────────────────────────────

    private AccountConfigService CreateService() => new(_tempDir);

    private void WriteConfig(string handle, AccountConfig config)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var json = JsonSerializer.Serialize(config, options);
        var path = Path.Combine(_tempDir, handle, "account.json");
        File.WriteAllText(path, json);
    }

    private static AccountConfig BuildConfig(string handle, string agentName) =>
        new()
        {
            Handle = handle,
            Agent = new AccountAgent { Name = agentName }
        };
}
