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
        _store = new JsonFileSessionStore(_testDir);
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
        session.CurrentState = OnboardingState.CollectBranding;
        await _store.SaveAsync(session, CancellationToken.None);
        var loaded = await _store.LoadAsync(session.Id, CancellationToken.None);
        Assert.Equal(OnboardingState.CollectBranding, loaded!.CurrentState);
    }
}
