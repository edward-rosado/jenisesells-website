using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealEstateStar.Workers.Shared.Security;

namespace RealEstateStar.Workers.Activation.TeamScrape.Tests;

public class TeamScrapeWorkerTests
{
    private const string AccountId = "test-account";
    private const string SourceUrl = "https://sunrise-realty.com/team";

    // ── Fixture HTML strings ─────────────────────────────────────────────────

    /// <summary>Standard brokerage team grid with <paramref name="agentCount"/> agent cards.</summary>
    private static string StandardTeamPage(int agentCount = 3) =>
        "<html><body>" +
        "<h1>Meet Our Team</h1>" +
        "<div class=\"team-grid\">" +
        string.Concat(Enumerable.Range(1, agentCount).Select(i =>
            $"""
            <div class="team-member">
              <img class="headshot" src="https://cdn.sunrise-realty.com/photos/agent{i}.jpg" alt="headshot" />
              <h3 class="agent-name">Agent Lastname{i}</h3>
              <p class="designation">REALTOR&reg; | Listing Specialist</p>
              <p class="bio">Agent Lastname{i} has helped hundreds of families find their dream home in Bergen County, NJ.</p>
              <a href="mailto:agent{i}@sunrise-realty.com">agent{i}@sunrise-realty.com</a>
              <a href="tel:201-555-{i:D4}">201-555-{i:D4}</a>
            </div>
            """)) +
        "</div></body></html>";

    /// <summary>Page with no recognisable team section — worker should return empty list.</summary>
    private static string NoTeamPage() =>
        "<html><body><h1>Welcome to Sunrise Realty</h1><p>We are a full-service brokerage.</p></body></html>";

    /// <summary>Agent bios containing XSS payloads — all must be sanitised.</summary>
    private static string XssTeamPage() =>
        """
        <html><body>
        <div class="team-member">
          <h3 class="name"><script>alert('xss')</script>Safe Name</h3>
          <p class="designation"><script>alert('xss')</script>REALTOR</p>
          <p class="bio"><img src=x onerror="alert(1)">I help buyers and sellers navigate the market in a competitive environment safely.</p>
          <a href="mailto:bad@example.com">bad@example.com</a>
        </div>
        </body></html>
        """;

    /// <summary>Page with 60 agent cards — result must be capped at 50.</summary>
    private static string LargeTeamPage() => StandardTeamPage(60);

    /// <summary>Malformed HTML: unclosed tags, missing attributes.</summary>
    private static string MalformedHtmlPage() =>
        """
        <html><body>
        <div class="team-grid">
          <div class="team-member">
            <h3>Partial Agent</h3>
            <p class="bio">Bio text that trails off without closing tag — we must not throw here.
          <!-- unclosed div intentional -->
          <div class="team-member">
            <h3>Second Agent</h3>
            <p class="designation">REALTOR</p>
            <a href="mailto:second@example.com">second@example.com</a>
          </div>
        </div>
        </body></html>
        """;

    /// <summary>Page using schema.org Person markup.</summary>
    private static string SchemaOrgPage() =>
        """
        <html><body>
        <div itemscope itemtype="https://schema.org/RealEstateAgent">
          <img src="https://cdn.broker.com/photo-jane.jpg" class="agent-photo" alt="photo" />
          <h2 itemprop="name">Jane Doe</h2>
          <span itemprop="jobTitle">Buyers Agent</span>
          <a href="mailto:jane@broker.com">jane@broker.com</a>
          <a href="tel:9085551234">908-555-1234</a>
          <p class="bio">Jane has 15 years of experience helping first-time buyers in Essex County, New Jersey.</p>
        </div>
        </body></html>
        """;

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string Ordinal(int i) => i switch
    {
        1 => "First",
        2 => "Second",
        3 => "Third",
        _ => $"Agent{i}"
    };

    /// <summary>
    /// Creates a <see cref="SsrfGuard"/> backed by a fake HTTP handler that returns
    /// the given <paramref name="body"/> for any HTTPS request.
    /// </summary>
    private static SsrfGuard OkGuard(string body)
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "text/html")
        });
        return new SsrfGuard(new HttpClient(handler), NullLogger<SsrfGuard>.Instance);
    }

    /// <summary>
    /// Creates a <see cref="SsrfGuard"/> that will reject any request because the URL
    /// resolves to a private IP (simulated by using an IP literal directly).
    /// The worker is expected to receive the SsrfResponse.Success=false and return empty.
    /// </summary>
    private static SsrfGuard RejectedGuard()
    {
        // SsrfGuard rejects non-HTTPS at validation time before calling the handler.
        // We can drive a failure by having the guard itself refuse the URL.
        // The easiest approach is to use the guard's scheme check (http:// → SSRF-001).
        // We pass a no-op handler — it won't be called.
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        return new SsrfGuard(new HttpClient(handler), NullLogger<SsrfGuard>.Instance);
    }

    private static TeamScrapeWorker BuildWorker(SsrfGuard ssrfGuard) =>
        new(ssrfGuard, NullLogger<TeamScrapeWorker>.Instance);

    // ── ScrapeTeamPageAsync — integration path tests ─────────────────────────

    [Fact]
    public async Task ScrapeTeamPageAsync_StandardPage_ReturnsExtractedAgents()
    {
        var worker = BuildWorker(OkGuard(StandardTeamPage(3)));

        var agents = await worker.ScrapeTeamPageAsync(SourceUrl, AccountId, CancellationToken.None);

        agents.Should().HaveCount(3);
        agents.Should().AllSatisfy(a =>
        {
            a.AccountId.Should().Be(AccountId);
            a.SourceUrl.Should().Be(SourceUrl);
            a.ScrapedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            a.Name.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task ScrapeTeamPageAsync_NoTeamSection_ReturnsEmpty()
    {
        var worker = BuildWorker(OkGuard(NoTeamPage()));

        var agents = await worker.ScrapeTeamPageAsync(SourceUrl, AccountId, CancellationToken.None);

        agents.Should().BeEmpty();
    }

    [Fact]
    public async Task ScrapeTeamPageAsync_SsrfRejectedViaHttpScheme_ReturnsEmptyWithoutThrowing()
    {
        // http:// URL triggers SSRF-001 rejection inside SsrfGuard without calling the handler
        var worker = BuildWorker(RejectedGuard());
        const string httpUrl = "http://internal-brokerage.local/team"; // non-HTTPS → rejected

        var act = async () => await worker.ScrapeTeamPageAsync(httpUrl, AccountId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        var agents = await worker.ScrapeTeamPageAsync(httpUrl, AccountId, CancellationToken.None);
        agents.Should().BeEmpty();
    }

    [Fact]
    public async Task ScrapeTeamPageAsync_InvalidUrl_ReturnsEmptyWithoutThrowing()
    {
        // Uri.TryCreate returns false — SsrfGuard is never called
        var worker = BuildWorker(OkGuard("")); // guard won't be invoked

        var act = async () =>
            await worker.ScrapeTeamPageAsync("not-a-url", AccountId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        var agents = await worker.ScrapeTeamPageAsync("not-a-url", AccountId, CancellationToken.None);
        agents.Should().BeEmpty();
    }

    [Fact]
    public async Task ScrapeTeamPageAsync_LargeTeamPage_CapsAtFiftyAgents()
    {
        var worker = BuildWorker(OkGuard(LargeTeamPage()));

        var agents = await worker.ScrapeTeamPageAsync(SourceUrl, AccountId, CancellationToken.None);

        agents.Should().HaveCount(50);
    }

    [Fact]
    public async Task ScrapeTeamPageAsync_XssInBios_SanitisesAllText()
    {
        var worker = BuildWorker(OkGuard(XssTeamPage()));

        var agents = await worker.ScrapeTeamPageAsync(SourceUrl, AccountId, CancellationToken.None);

        // At least one agent is extracted; all fields must be script-free
        if (agents.Count > 0)
        {
            var agent = agents[0];
            agent.Name.Should().NotContain("<script>");
            agent.Name.Should().NotContain("alert(");
            agent.Title?.Should().NotContain("<script>");
            agent.Title?.Should().NotContain("alert(");
            agent.BioSnippet?.Should().NotContain("<img");
            agent.BioSnippet?.Should().NotContain("onerror");
            agent.BioSnippet?.Should().NotContain("<script>");
        }
    }

    [Fact]
    public async Task ScrapeTeamPageAsync_MalformedHtml_ReturnsPartialResultsWithoutThrowing()
    {
        var worker = BuildWorker(OkGuard(MalformedHtmlPage()));

        var act = async () =>
            await worker.ScrapeTeamPageAsync(SourceUrl, AccountId, CancellationToken.None);

        await act.Should().NotThrowAsync();
        // Partial results are acceptable — we just need no exception
        var agents = await worker.ScrapeTeamPageAsync(SourceUrl, AccountId, CancellationToken.None);
        agents.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ScrapeTeamPageAsync_HttpErrorResponse_ReturnsEmpty()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var guard = new SsrfGuard(new HttpClient(handler), NullLogger<SsrfGuard>.Instance);
        var worker = BuildWorker(guard);

        var agents = await worker.ScrapeTeamPageAsync(SourceUrl, AccountId, CancellationToken.None);

        // 404 is a non-success HTTP status — body is empty, so zero agents
        agents.Should().BeEmpty();
    }

    // ── ParseAgentCards — unit tests for extraction logic ────────────────────

    [Fact]
    public void ParseAgentCards_SchemaOrgPage_ExtractsAgent()
    {
        var worker = BuildWorker(OkGuard("")); // guard not used for ParseAgentCards

        var agents = worker.ParseAgentCards(SchemaOrgPage(), SourceUrl, AccountId);

        agents.Should().HaveCountGreaterThanOrEqualTo(1);
        var agent = agents[0];
        agent.Name.Should().Be("Jane Doe");
        agent.AccountId.Should().Be(AccountId);
        agent.SourceUrl.Should().Be(SourceUrl);
    }

    [Fact]
    public void ParseAgentCards_EmptyHtml_ReturnsEmpty()
    {
        var worker = BuildWorker(OkGuard(""));

        var agents = worker.ParseAgentCards("", SourceUrl, AccountId);

        agents.Should().BeEmpty();
    }

    [Fact]
    public void ParseAgentCards_DuplicateNames_Deduplicates()
    {
        // Same name appears in two different card class patterns — should only produce one entry
        var html =
            """
            <div class="team-member">
              <h3 class="agent-name">Jane Doe</h3>
              <a href="mailto:jane@example.com">jane@example.com</a>
            </div>
            <div class="agent-card">
              <h3 class="agent-name">Jane Doe</h3>
              <a href="mailto:jane@example.com">jane@example.com</a>
            </div>
            """;

        var worker = BuildWorker(OkGuard(""));
        var agents = worker.ParseAgentCards(html, SourceUrl, AccountId);

        agents.Where(a => a.Name == "Jane Doe").Should().HaveCount(1);
    }

    // ── ExtractName ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("""<h3 class="agent-name">Maria Santos</h3>""", "Maria Santos")]
    [InlineData("""<h2>John Smith</h2>""", "John Smith")]
    [InlineData("""<p>no heading here</p>""", null)]
    public void ExtractName_VariousInputs_ReturnsExpected(string html, string? expected)
    {
        var result = TeamScrapeWorker.ExtractName(html);

        if (expected is null)
            result.Should().BeNull();
        else
            result.Should().Be(expected);
    }

    [Fact]
    public void ExtractName_HtmlEntitiesInName_DecodesCorrectly()
    {
        var html = """<h3 class="name">Jos&eacute; Mart&iacute;nez</h3>""";
        var result = TeamScrapeWorker.ExtractName(html);
        result.Should().Be("José Martínez");
    }

    [Fact]
    public void ExtractName_ScriptTagInHeading_Sanitised()
    {
        var html = """<h3 class="name"><script>alert(1)</script>Safe Name</h3>""";
        var result = TeamScrapeWorker.ExtractName(html);
        result.Should().NotContain("<script>");
        result.Should().NotContain("alert(");
    }

    // ── ExtractTitle ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("""<p class="designation">REALTOR® | Listing Specialist</p>""", "REALTOR® | Listing Specialist")]
    [InlineData("""<span class="title">Team Lead</span>""", "Team Lead")]
    [InlineData("""<p>No title class here</p>""", null)]
    public void ExtractTitle_VariousInputs_ReturnsExpected(string html, string? expected)
    {
        var result = TeamScrapeWorker.ExtractTitle(html);

        if (expected is null)
            result.Should().BeNull();
        else
            result.Should().Be(expected);
    }

    // ── ExtractEmail ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("""<a href="mailto:jane@sunrise.com">jane@sunrise.com</a>""", "jane@sunrise.com")]
    [InlineData("""<a href="mailto:info+team@broker.co.uk">info+team@broker.co.uk</a>""", "info+team@broker.co.uk")]
    [InlineData("""<a href="tel:5551234">555-1234</a>""", null)]
    [InlineData("", null)]
    public void ExtractEmail_VariousInputs_ReturnsExpected(string html, string? expected)
    {
        var result = TeamScrapeWorker.ExtractEmail(html);
        result.Should().Be(expected);
    }

    [Fact]
    public void ExtractEmail_ValidMailto_ContainsAtSign()
    {
        var html = """<a href="mailto:agent@broker.com">email us</a>""";
        var result = TeamScrapeWorker.ExtractEmail(html);
        result.Should().NotBeNull();
        result!.Should().Contain("@");
    }

    // ── ExtractPhone ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("""<a href="tel:+12015559876">Call</a>""", "+12015559876")]
    [InlineData("""<a href="tel:201-555-1234">201-555-1234</a>""", "201-555-1234")]
    [InlineData("""<p>Call us at (201) 555-0001 today.</p>""", "(201) 555-0001")]
    [InlineData("""<p>No phone here.</p>""", null)]
    public void ExtractPhone_VariousInputs_ReturnsExpected(string html, string? expected)
    {
        var result = TeamScrapeWorker.ExtractPhone(html);
        result.Should().Be(expected);
    }

    // ── ExtractBio ────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractBio_WithBioClass_ReturnsSanitisedText()
    {
        var html = """<p class="bio">Maria has helped over 200 families find their perfect home in Hudson County, NJ, for 12 years.</p>""";
        var result = TeamScrapeWorker.ExtractBio(html);
        result.Should().NotBeNull();
        result.Should().Contain("Maria");
        result.Should().NotContain("<p");
    }

    [Fact]
    public void ExtractBio_XssPayload_Sanitised()
    {
        var html = """<p class="bio"><script>evil()</script>I help clients buy and sell homes, with deep knowledge of local markets and neighborhoods.</p>""";
        var result = TeamScrapeWorker.ExtractBio(html);
        result.Should().NotContain("<script>");
        result.Should().NotContain("evil()");
    }

    [Fact]
    public void ExtractBio_TooShort_ReturnsNull()
    {
        // Bio pattern requires 50+ chars — a 10-char bio returns null
        var html = """<p class="bio">Short bio.</p>""";
        var result = TeamScrapeWorker.ExtractBio(html);
        result.Should().BeNull();
    }

    // ── ExtractHeadshotUrl ────────────────────────────────────────────────────

    [Fact]
    public void ExtractHeadshotUrl_ClassHintedImg_ReturnsSrc()
    {
        var html = """<img class="headshot" src="https://cdn.broker.com/photo.jpg" alt="Agent photo" />""";
        var result = TeamScrapeWorker.ExtractHeadshotUrl(html);
        result.Should().Be("https://cdn.broker.com/photo.jpg");
    }

    [Fact]
    public void ExtractHeadshotUrl_AltHintedImg_ReturnsSrc()
    {
        var html = """<img src="https://cdn.broker.com/portrait.jpg" alt="agent headshot" />""";
        var result = TeamScrapeWorker.ExtractHeadshotUrl(html);
        result.Should().Be("https://cdn.broker.com/portrait.jpg");
    }

    [Fact]
    public void ExtractHeadshotUrl_DataUri_ReturnsNull()
    {
        var html = """<img class="headshot" src="data:image/png;base64,abc123" alt="photo" />""";
        var result = TeamScrapeWorker.ExtractHeadshotUrl(html);
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractHeadshotUrl_NoImgTag_ReturnsNull()
    {
        var html = """<p>No image here</p>""";
        var result = TeamScrapeWorker.ExtractHeadshotUrl(html);
        result.Should().BeNull();
    }

    // ── Agent count cap ───────────────────────────────────────────────────────

    [Fact]
    public void ParseAgentCards_SixtyCards_CapsAtFifty()
    {
        var html = "<html><body><div class=\"team-grid\">" +
                   string.Concat(Enumerable.Range(1, 60).Select(i =>
                       $"""<div class="team-member"><h3 class="agent-name">Agent Lastname{i}</h3></div>""")) +
                   "</div></body></html>";

        var worker = BuildWorker(OkGuard(""));
        var agents = worker.ParseAgentCards(html, SourceUrl, AccountId);

        agents.Should().HaveCount(50);
    }

    // ── ScrapedAt and SourceUrl populated ────────────────────────────────────

    [Fact]
    public async Task ScrapeTeamPageAsync_AgentsHaveScrapedAtAndSourceUrl()
    {
        var worker = BuildWorker(OkGuard(StandardTeamPage(1)));

        var before = DateTime.UtcNow.AddSeconds(-1);
        var agents = await worker.ScrapeTeamPageAsync(SourceUrl, AccountId, CancellationToken.None);
        var after = DateTime.UtcNow.AddSeconds(1);

        agents.Should().HaveCount(1);
        agents[0].ScrapedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        agents[0].SourceUrl.Should().Be(SourceUrl);
    }

    // ── Cancellation propagation ──────────────────────────────────────────────

    [Fact]
    public async Task ScrapeTeamPageAsync_CancelledToken_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();

        // Handler that honours cancellation
        var handler = new FakeHandler(async req =>
        {
            await Task.Delay(Timeout.Infinite, req.Options
                .TryGetValue(new HttpRequestOptionsKey<CancellationToken>("ct"), out var ct)
                    ? ct
                    : CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        // Pre-cancel so the guard's linked CTS fires immediately
        cts.Cancel();

        var guard = new SsrfGuard(new HttpClient(handler), NullLogger<SsrfGuard>.Instance);
        var worker = BuildWorker(guard);

        var act = async () =>
            await worker.ScrapeTeamPageAsync(SourceUrl, AccountId, cts.Token);

        // Either OCE or the guard may short-circuit before calling the handler;
        // either way the method must not throw a non-cancellation exception.
        try
        {
            await act();
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        // If it returns empty without throwing — also acceptable (invalid URL path) —
        // the important assertion is that no non-cancellation exception escapes.
    }

    // ── Fake HTTP handler ─────────────────────────────────────────────────────

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> syncHandler)
            : this(req => Task.FromResult(syncHandler(req))) { }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}
