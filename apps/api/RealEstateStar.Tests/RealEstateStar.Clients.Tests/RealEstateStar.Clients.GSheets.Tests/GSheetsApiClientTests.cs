using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Clients.GoogleOAuth;
using RealEstateStar.Domain.Shared;
using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.TestUtilities;

namespace RealEstateStar.Clients.GSheets.Tests;

/// <summary>
/// Tests for GSheetsApiClient.
///
/// Note: SheetsService.Spreadsheets.Values.* makes live HTTP calls to Google's API and cannot
/// be unit-tested without a full Google API mock framework. The throw paths (missing token,
/// refresh failure) are fully exercised without hitting the Google API, and diagnostics counters
/// are verified for the token-missing path.
/// </summary>
public class GSheetsApiClientTests
{
    private const string AccountId = "test-account";
    private const string AgentId = "test-agent";
    private const string ClientId = "test-client-id";
    private const string ClientSecret = "test-client-secret";
    private const string SpreadsheetId = "spreadsheet-123";
    private const string SheetName = "Sheet1";

    private static OAuthCredential ValidCredential() => new()
    {
        AccountId = AccountId,
        AgentId = AgentId,
        AccessToken = "access-token",
        RefreshToken = "refresh-token",
        ExpiresAt = DateTime.UtcNow.AddHours(1),
        Scopes = ["https://www.googleapis.com/auth/spreadsheets"],
        Email = "agent@example.com",
        Name = "Test Agent",
        ETag = "etag-123"
    };

    private static (GSheetsApiClient Client, InMemoryTokenStore Store, MockHttpMessageHandler OAuthHandler)
        BuildClient()
    {
        var store = new InMemoryTokenStore();
        var oauthHandler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(oauthHandler);
        var refresher = new GoogleOAuthRefresher(
            store, ClientId, ClientSecret, httpClient, NullLogger<GoogleOAuthRefresher>.Instance);
        var client = new GSheetsApiClient(refresher, ClientId, ClientSecret, NullLogger<GSheetsApiClient>.Instance);
        return (client, store, oauthHandler);
    }

    // ──────────────────────────────────────────────────────────
    // AppendRowAsync — throws when no token / refresh fails
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AppendRowAsync_Throws_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var act = async () => await client.AppendRowAsync(
            AccountId, AgentId, SpreadsheetId, SheetName,
            ["col1", "col2"], CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AppendRowAsync_IncrementsTokenMissingCounter_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();
        long captured = 0;

        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == GSheetsDiagnostics.ServiceName &&
                instrument.Name == "gsheets.token_missing")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "gsheets.token_missing")
                Interlocked.Add(ref captured, measurement);
        });
        listener.Start();

        // AppendRowAsync now throws after incrementing the counter — catch the exception
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.AppendRowAsync(
                AccountId, AgentId, SpreadsheetId, SheetName,
                ["col1"], CancellationToken.None));

        captured.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task AppendRowAsync_Throws_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();

        var expiredCredential = ValidCredential() with
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            ETag = "old-etag"
        };
        await store.SaveAsync(expiredCredential, OAuthProviders.Google, CancellationToken.None);

        oauthHandler.ResponseToReturn = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\": \"invalid_grant\"}", System.Text.Encoding.UTF8, "application/json")
        };

        var act = async () => await client.AppendRowAsync(
            AccountId, AgentId, SpreadsheetId, SheetName,
            ["col1"], CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ──────────────────────────────────────────────────────────
    // ReadRowsAsync — still returns empty when no token (silent path)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadRowsAsync_ReturnsEmpty_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var result = await client.ReadRowsAsync(
            AccountId, AgentId, SpreadsheetId, SheetName, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadRowsAsync_IncrementsTokenMissingCounter_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();
        long captured = 0;

        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == GSheetsDiagnostics.ServiceName &&
                instrument.Name == "gsheets.token_missing")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "gsheets.token_missing")
                Interlocked.Add(ref captured, measurement);
        });
        listener.Start();

        await client.ReadRowsAsync(
            AccountId, AgentId, SpreadsheetId, SheetName, CancellationToken.None);

        captured.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ReadRowsAsync_ReturnsEmpty_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();

        var expiredCredential = ValidCredential() with
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            ETag = "old-etag"
        };
        await store.SaveAsync(expiredCredential, OAuthProviders.Google, CancellationToken.None);

        oauthHandler.ResponseToReturn = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\": \"invalid_grant\"}", System.Text.Encoding.UTF8, "application/json")
        };

        var result = await client.ReadRowsAsync(
            AccountId, AgentId, SpreadsheetId, SheetName, CancellationToken.None);

        result.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────
    // RedactRowsAsync — throws when no token / refresh fails
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RedactRowsAsync_Throws_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();

        var act = async () => await client.RedactRowsAsync(
            AccountId, AgentId, SpreadsheetId, SheetName,
            "Email", "buyer@example.com", "[REDACTED]", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RedactRowsAsync_IncrementsTokenMissingCounter_WhenTokenMissing()
    {
        var (client, _, _) = BuildClient();
        long captured = 0;

        using var listener = new System.Diagnostics.Metrics.MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == GSheetsDiagnostics.ServiceName &&
                instrument.Name == "gsheets.token_missing")
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "gsheets.token_missing")
                Interlocked.Add(ref captured, measurement);
        });
        listener.Start();

        // RedactRowsAsync now throws after incrementing the counter — catch the exception
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.RedactRowsAsync(
                AccountId, AgentId, SpreadsheetId, SheetName,
                "Email", "buyer@example.com", "[REDACTED]", CancellationToken.None));

        captured.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task RedactRowsAsync_Throws_WhenRefreshFails()
    {
        var (client, store, oauthHandler) = BuildClient();

        var expiredCredential = ValidCredential() with
        {
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            ETag = "old-etag"
        };
        await store.SaveAsync(expiredCredential, OAuthProviders.Google, CancellationToken.None);

        oauthHandler.ResponseToReturn = new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\": \"invalid_grant\"}", System.Text.Encoding.UTF8, "application/json")
        };

        var act = async () => await client.RedactRowsAsync(
            AccountId, AgentId, SpreadsheetId, SheetName,
            "Email", "buyer@example.com", "[REDACTED]", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

}
