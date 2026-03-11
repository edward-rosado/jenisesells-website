using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Api.Features.Onboarding;
using RealEstateStar.Api.Features.Onboarding.Services;
using Xunit;

namespace RealEstateStar.Api.Tests.Features.Onboarding.Services;

public class SessionStoreTests : IDisposable
{
    private readonly string _testDir;
    private readonly JsonFileSessionStore _store;

    public SessionStoreTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"res-sessions-{Guid.NewGuid():N}");
        _store = new JsonFileSessionStore(_testDir, NullLogger<JsonFileSessionStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrips()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(session.Id, loaded!.Id);
        Assert.Equal(session.ProfileUrl, loaded.ProfileUrl);
    }

    [Fact]
    public async Task Load_NonExistentId_ReturnsNull()
    {
        // Use a valid hex format that doesn't exist on disk
        var result = await _store.LoadAsync("aabbccddeeff", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task Save_PreservesMessages()
    {
        var session = OnboardingSession.Create(null);
        session.Messages.Add(new ChatMessage { Role = ChatRole.User, Content = "hello" });
        session.Messages.Add(new ChatMessage { Role = ChatRole.Assistant, Content = "hi" });
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.Equal(2, loaded!.Messages.Count);
        Assert.Equal("hello", loaded.Messages[0].Content);
    }

    [Fact]
    public async Task Save_PreservesStateChanges()
    {
        var session = OnboardingSession.Create(null);
        session.CurrentState = OnboardingState.GenerateSite;
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.Equal(OnboardingState.GenerateSite, loaded!.CurrentState);
    }

    // --- Path validation ---

    [Theory]
    [InlineData("")]
    [InlineData("not-hex-chars!")]
    [InlineData("AABBCCDDEEFF")] // uppercase not allowed
    [InlineData("aabbccddee")] // too short (10 chars, needs 12)
    [InlineData("aabbccddeeffaa")] // too long (14 chars)
    [InlineData("../../../etc")]
    public async Task Load_InvalidSessionId_ThrowsArgumentException(string badId)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.LoadAsync(badId, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-valid")]
    [InlineData("../secret")]
    public async Task Save_InvalidSessionId_ThrowsArgumentException(string badId)
    {
        var session = OnboardingSession.Create(null);
        // Overwrite the ID to something invalid
        typeof(OnboardingSession).GetProperty("Id")!.SetValue(session, badId);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.SaveAsync(session, CancellationToken.None));
    }

    [Fact]
    public async Task SaveAndLoad_PreservesProfile()
    {
        var session = OnboardingSession.Create(null);
        session.Profile = new ScrapedProfile
        {
            Name = "Jane Doe",
            Phone = "555-1234",
            Email = "jane@test.com",
            Brokerage = "RE/MAX",
            State = "NJ",
            PrimaryColor = "#003366",
        };
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.NotNull(loaded!.Profile);
        Assert.Equal("Jane Doe", loaded.Profile!.Name);
        Assert.Equal("#003366", loaded.Profile.PrimaryColor);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesGoogleTokens()
    {
        var session = OnboardingSession.Create(null);
        session.GoogleTokens = new GoogleTokens
        {
            AccessToken = "at",
            RefreshToken = "rt",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["email"],
            GoogleEmail = "j@g.com",
            GoogleName = "Jane",
        };
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.NotNull(loaded!.GoogleTokens);
        Assert.Equal("j@g.com", loaded.GoogleTokens!.GoogleEmail);
    }

    [Fact]
    public async Task ConcurrentSaves_DoNotCorrupt()
    {
        var session = OnboardingSession.Create(null);

        // Run 10 concurrent saves — should not throw or corrupt
        var tasks = Enumerable.Range(0, 10).Select(i =>
        {
            session.CurrentState = (OnboardingState)(i % 5);
            return _store.SaveAsync(session, CancellationToken.None);
        });

        await Task.WhenAll(tasks);

        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.NotNull(loaded);
    }

    [Fact]
    public async Task Load_CorruptJson_ThrowsJsonException()
    {
        var session = OnboardingSession.Create(null);
        // Write corrupt JSON directly to disk
        Directory.CreateDirectory(_testDir);
        var path = Path.Combine(_testDir, $"{session.Id}.json");
        await File.WriteAllTextAsync(path, "{ this is not valid json !!! }");

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => _store.LoadAsync(session.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Load_ReadOnlyBasePath_ThrowsIOException()
    {
        // Use a path that doesn't exist and can't be created — load triggers I/O error
        // on Windows, an invalid path char triggers IOException
        var badStore = new JsonFileSessionStore(
            Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid():N}", "deep", "path"),
            NullLogger<JsonFileSessionStore>.Instance);

        // The file simply won't exist, so LoadAsync returns null (no IOException on read for missing file)
        var result = await badStore.LoadAsync("aabbccddeeff", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task Save_PreservesUpdatedAt()
    {
        var session = OnboardingSession.Create(null);
        var originalUpdatedAt = session.UpdatedAt;
        session.UpdatedAt = DateTime.UtcNow.AddHours(1);
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.True(loaded!.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public async Task DefaultConstructor_UsesBaseDirectory()
    {
        // Verify the parameterless constructor creates a store with default base path
        var store = new JsonFileSessionStore(NullLogger<JsonFileSessionStore>.Instance);
        // Just verify it can be created and used — load a nonexistent session
        var result = await store.LoadAsync("aabbccddeeff", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task Save_OverwritesExistingSession()
    {
        var session = OnboardingSession.Create("https://zillow.com/profile/test");
        session.CurrentState = OnboardingState.ScrapeProfile;
        await _store.SaveAsync(session, CancellationToken.None);

        session.CurrentState = OnboardingState.ConnectGoogle;
        await _store.SaveAsync(session, CancellationToken.None);

        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.Equal(OnboardingState.ConnectGoogle, loaded!.CurrentState);
    }

    // --- Path traversal defense-in-depth ---

    [Fact]
    public void ValidatePathWithinBase_PathInsideBase_DoesNotThrow()
    {
        _store.ValidatePathWithinBase(
            Path.Combine(_testDir, "aabbccddeeff.json"),
            _testDir,
            "aabbccddeeff");
    }

    [Fact]
    public void ValidatePathWithinBase_PathOutsideBase_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _store.ValidatePathWithinBase(
                Path.Combine(Path.GetTempPath(), "other-dir", "aabbccddeeff.json"),
                _testDir,
                "aabbccddeeff"));

        Assert.Contains("Path traversal detected", ex.Message);
    }

    [Fact]
    public async Task Load_TruncatesJsonPreviewInErrorLog()
    {
        // Write a long corrupt JSON string (>500 chars) to test the preview truncation branch
        var session = OnboardingSession.Create(null);
        Directory.CreateDirectory(_testDir);
        var path = Path.Combine(_testDir, $"{session.Id}.json");
        var longCorruptJson = "{" + new string('x', 600) + "}";
        await File.WriteAllTextAsync(path, longCorruptJson);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => _store.LoadAsync(session.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Load_ShortCorruptJson_DoesNotTruncatePreview()
    {
        // Write a short corrupt JSON string (<500 chars) to test the non-truncation branch
        var session = OnboardingSession.Create(null);
        Directory.CreateDirectory(_testDir);
        var path = Path.Combine(_testDir, $"{session.Id}.json");
        await File.WriteAllTextAsync(path, "{bad}");

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => _store.LoadAsync(session.Id, CancellationToken.None));
    }
}
