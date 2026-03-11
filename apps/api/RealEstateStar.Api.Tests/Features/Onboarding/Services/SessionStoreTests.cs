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
}
