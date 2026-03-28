using Xunit;
using Moq;
using FluentAssertions;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.Domain.Leads;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.WhatsApp.Interfaces;
using RealEstateStar.Domain.Onboarding.Models;
using RealEstateStar.Domain.Onboarding.Interfaces;
using RealEstateStar.DataServices.Config;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Onboarding;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.DataServices.WhatsApp;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;

namespace RealEstateStar.DataServices.Tests.Onboarding;

public class SessionStoreEncryptionTests : IDisposable
{
    private readonly string _testDir;
    private readonly SessionDataService _innerStore;
    private readonly EncryptingSessionDecorator _sut;
    private readonly IDataProtectionProvider _provider;

    public SessionStoreEncryptionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"res-enc-{Guid.NewGuid():N}");
        _provider = new EphemeralDataProtectionProvider();
        _innerStore = new SessionDataService(_testDir, NullLogger<SessionDataService>.Instance);
        _sut = new EncryptingSessionDecorator(
            _innerStore,
            _provider,
            NullLogger<EncryptingSessionDecorator>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private static OnboardingSession CreateSessionWithTokens()
    {
        var session = OnboardingSession.Create(null);
        session.GoogleTokens = new OAuthCredential
        {
            AccessToken = "test-access-token-12345",
            RefreshToken = "test-refresh-token-67890",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            Scopes = ["email", "drive"],
            Email = "test@gmail.com",
            Name = "Test User",
        };
        return session;
    }

    [Fact]
    public async Task SaveAndLoad_EncryptsAndDecryptsTokens()
    {
        var session = CreateSessionWithTokens();
        var originalAccess = session.GoogleTokens!.AccessToken;
        var originalRefresh = session.GoogleTokens!.RefreshToken;

        await _sut.SaveAsync(session, CancellationToken.None);
        var loaded = await _sut.LoadAsync(session.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.GoogleTokens);
        Assert.Equal(originalAccess, loaded.GoogleTokens!.AccessToken);
        Assert.Equal(originalRefresh, loaded.GoogleTokens.RefreshToken);
    }

    [Fact]
    public async Task SaveAndLoad_NullGoogleTokens_NoError()
    {
        var session = OnboardingSession.Create(null);
        Assert.Null(session.GoogleTokens);

        await _sut.SaveAsync(session, CancellationToken.None);
        var loaded = await _sut.LoadAsync(session.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.GoogleTokens);
    }

    [Fact]
    public async Task Load_PlaintextTokens_MigrationFallback()
    {
        var session = CreateSessionWithTokens();
        var originalAccess = session.GoogleTokens!.AccessToken;
        var originalRefresh = session.GoogleTokens!.RefreshToken;

        // Write directly via inner store (no encryption)
        await _innerStore.SaveAsync(session, CancellationToken.None);

        // Load via decorator — should NOT crash, treats as plaintext migration
        var loaded = await _sut.LoadAsync(session.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.NotNull(loaded!.GoogleTokens);
        Assert.Equal(originalAccess, loaded.GoogleTokens!.AccessToken);
        Assert.Equal(originalRefresh, loaded.GoogleTokens.RefreshToken);
    }

    [Fact]
    public async Task Save_DoesNotEncryptOtherFields()
    {
        var session = CreateSessionWithTokens();
        session.Messages.Add(new ChatMessage { Role = ChatRole.User, Content = "hello world" });
        session.Profile = new ScrapedProfile
        {
            Name = "Test Agent",
            Phone = "555-1234",
            Email = "agent@test.com",
            Brokerage = "RE/MAX",
            State = "NJ",
        };

        await _sut.SaveAsync(session, CancellationToken.None);

        // Read raw JSON from disk
        var rawJson = await File.ReadAllTextAsync(Path.Combine(_testDir, $"{session.Id}.json"));

        // Profile and messages should be plaintext
        Assert.Contains("Test Agent", rawJson);
        Assert.Contains("hello world", rawJson);
        Assert.Contains("agent@test.com", rawJson);

        // Tokens should NOT be plaintext
        Assert.DoesNotContain("test-access-token-12345", rawJson);
        Assert.DoesNotContain("test-refresh-token-67890", rawJson);

        // Other token fields should still be plaintext
        Assert.Contains("test@gmail.com", rawJson);
        Assert.Contains("Test User", rawJson);
    }

    [Fact]
    public async Task Save_RestoresOriginalTokensInMemory()
    {
        var session = CreateSessionWithTokens();
        var originalAccess = session.GoogleTokens!.AccessToken;
        var originalRefresh = session.GoogleTokens!.RefreshToken;

        await _sut.SaveAsync(session, CancellationToken.None);

        // In-memory session should still have original plaintext tokens
        Assert.Equal(originalAccess, session.GoogleTokens!.AccessToken);
        Assert.Equal(originalRefresh, session.GoogleTokens.RefreshToken);
    }

    [Fact]
    public async Task EncryptedValues_NotDeterministic()
    {
        // Save two sessions with identical tokens to different IDs
        var session1 = CreateSessionWithTokens();
        var session2 = CreateSessionWithTokens();

        await _sut.SaveAsync(session1, CancellationToken.None);
        await _sut.SaveAsync(session2, CancellationToken.None);

        var raw1 = await File.ReadAllTextAsync(Path.Combine(_testDir, $"{session1.Id}.json"));
        var raw2 = await File.ReadAllTextAsync(Path.Combine(_testDir, $"{session2.Id}.json"));

        // Parse out the accessToken values from JSON
        using var doc1 = JsonDocument.Parse(raw1);
        using var doc2 = JsonDocument.Parse(raw2);

        var encAccess1 = doc1.RootElement.GetProperty("googleTokens").GetProperty("accessToken").GetString();
        var encAccess2 = doc2.RootElement.GetProperty("googleTokens").GetProperty("accessToken").GetString();

        // Encrypted values should differ (DPAPI uses random IV)
        Assert.NotNull(encAccess1);
        Assert.NotNull(encAccess2);
        Assert.NotEqual(encAccess1, encAccess2);
    }

    [Fact]
    public async Task Load_NonExistentSession_ReturnsNull()
    {
        var result = await _sut.LoadAsync("aabbccddeeff", CancellationToken.None);
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesNonTokenFields()
    {
        var session = CreateSessionWithTokens();
        session.CurrentState = OnboardingState.ConnectGoogle;
        session.Messages.Add(new ChatMessage { Role = ChatRole.User, Content = "test message" });

        await _sut.SaveAsync(session, CancellationToken.None);
        var loaded = await _sut.LoadAsync(session.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(OnboardingState.ConnectGoogle, loaded!.CurrentState);
        Assert.Single(loaded.Messages);
        Assert.Equal("test message", loaded.Messages[0].Content);
        Assert.Equal("test@gmail.com", loaded.GoogleTokens!.Email);
        Assert.Equal(["email", "drive"], loaded.GoogleTokens.Scopes);
    }
}
