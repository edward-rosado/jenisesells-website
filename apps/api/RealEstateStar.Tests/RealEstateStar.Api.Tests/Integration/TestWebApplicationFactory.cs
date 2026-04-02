using RealEstateStar.Domain.Shared.Models;
using RealEstateStar.Domain.Leads.Models;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Cma.Models;
using RealEstateStar.Domain.Activation.Models;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.HomeSearch.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace RealEstateStar.Api.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory that injects test configuration values
/// so Program.cs startup validation doesn't throw in CI.
/// Uses UseSetting which adds to the host configuration before builder.Build().
/// Also registers no-op stubs for all lead services that require external storage
/// so integration tests can run without real GDrive/GWS credentials.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting injects into the configuration before Program.cs reads it
        builder.UseSetting("Anthropic:ApiKey", "test-anthropic-key");
        builder.UseSetting("Google:ClientId", "test-google-client-id");
        builder.UseSetting("Google:ClientSecret", "test-google-client-secret");
        builder.UseSetting("Google:RedirectUri", "http://localhost:5000/oauth/google/callback");
        builder.UseSetting("Stripe:SecretKey", "sk_test_placeholder");
        builder.UseSetting("Stripe:PriceId", "price_test_placeholder");
        builder.UseSetting("Stripe:WebhookSecret", "whsec_test_placeholder");
        builder.UseSetting("Platform:BaseUrl", "http://localhost:3000");

        // Register no-op stubs for lead services (require external GDrive/GWS credentials in production).
        // Subclasses may override these with more specific implementations.
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<ILeadStore, NoOpLeadStore>();
            services.AddSingleton<IMarketingConsentLog, NoOpMarketingConsentLog>();
            // TODO: Pipeline redesign — ILeadEnricher and ILeadNotifier removed in Phase 1.5; replaced in Phase 2/3/4
            // services.AddSingleton<ILeadEnricher, NoOpLeadEnricher>();
            // services.AddSingleton<ILeadNotifier, NoOpLeadNotifier>();
            services.AddSingleton<IHomeSearchProvider, NoOpHomeSearchProvider>();
            services.AddSingleton<IHomeSearchNotifier, NoOpHomeSearchNotifier>();
            services.AddSingleton<ICompAggregator, NoOpCompAggregator>();
            services.AddSingleton<ICmaAnalyzer, NoOpCmaAnalyzer>();
            services.AddSingleton<ICmaPdfGenerator, NoOpCmaPdfGenerator>();
            services.AddSingleton<ICmaNotifier, NoOpCmaNotifier>();
            services.AddSingleton<ILeadDataDeletion, NoOpLeadDataDeletion>();
            services.AddSingleton<IDeletionAuditLog, NoOpDeletionAuditLog>();
        });
    }
}

// ---------------------------------------------------------------------------
// No-op stubs — used in all integration tests as default lead service stubs.
// Subclasses override with configured mocks for behaviour-specific tests.
// ---------------------------------------------------------------------------

file sealed class NoOpLeadStore : ILeadStore
{
    public Task SaveAsync(Lead lead, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateScoreAsync(Lead l, LeadScore s, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateHomeSearchIdAsync(string a, Guid i, string h, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateStatusAsync(Lead l, LeadStatus s, CancellationToken ct) => Task.CompletedTask;
    public Task UpdateMarketingOptInAsync(string a, Guid i, bool o, CancellationToken ct) => Task.CompletedTask;
    public Task<Lead?> GetAsync(string a, Guid i, CancellationToken ct) => Task.FromResult<Lead?>(null);
    public Task<Lead?> GetByNameAsync(string a, string n, CancellationToken ct) => Task.FromResult<Lead?>(null);
    public Task<Lead?> GetByEmailAsync(string a, string e, CancellationToken ct) => Task.FromResult<Lead?>(null);
    public Task<List<Lead>> ListByStatusAsync(string a, LeadStatus s, CancellationToken ct) => Task.FromResult(new List<Lead>());
    public Task DeleteAsync(string a, Guid i, CancellationToken ct) => Task.CompletedTask;
}

file sealed class NoOpMarketingConsentLog : IMarketingConsentLog
{
    public Task RecordConsentAsync(string agentId, MarketingConsent consent, CancellationToken ct) => Task.CompletedTask;
    public Task RedactAsync(string agentId, string email, CancellationToken ct) => Task.CompletedTask;
}

// TODO: Pipeline redesign — ILeadEnricher and ILeadNotifier removed in Phase 1.5; replaced in Phase 2/3/4
// NoOpLeadEnricher and NoOpLeadNotifier removed

file sealed class NoOpHomeSearchProvider : IHomeSearchProvider
{
    public Task<List<Listing>> SearchAsync(HomeSearchCriteria criteria, CancellationToken ct) =>
        Task.FromResult(new List<Listing>());
}

file sealed class NoOpLeadDataDeletion : ILeadDataDeletion
{
    public Task<string> InitiateDeletionRequestAsync(string agentId, string email, CancellationToken ct) =>
        Task.FromResult("no-op-token");
    public Task<DeleteResult> ExecuteDeletionAsync(string agentId, string email, string token, string reason, CancellationToken ct) =>
        Task.FromResult(new DeleteResult(true, []));
}

file sealed class NoOpDeletionAuditLog : IDeletionAuditLog
{
    public Task RecordInitiationAsync(string agentId, Guid leadId, string email, CancellationToken ct) => Task.CompletedTask;
    public Task RecordCompletionAsync(string agentId, Guid leadId, CancellationToken ct) => Task.CompletedTask;
}

file sealed class NoOpHomeSearchNotifier : IHomeSearchNotifier
{
    public Task NotifyBuyerAsync(string agentId, Lead lead, List<Listing> listings, string correlationId, CancellationToken ct) =>
        Task.CompletedTask;
}

file sealed class NoOpCompAggregator : ICompAggregator
{
    public Task<List<Comp>> FetchCompsAsync(CompSearchRequest request, CancellationToken ct) =>
        Task.FromResult(new List<Comp>());
}

file sealed class NoOpCmaAnalyzer : ICmaAnalyzer
{
    public Task<CmaAnalysis> AnalyzeAsync(Lead lead, List<Comp> comps, CancellationToken ct, AgentContext? agentContext = null) =>
        Task.FromResult(new CmaAnalysis
        {
            ValueLow = 0,
            ValueMid = 0,
            ValueHigh = 0,
            MarketNarrative = "no-op",
            MarketTrend = "Balanced",
            MedianDaysOnMarket = 0
        });
}

file sealed class NoOpCmaPdfGenerator : ICmaPdfGenerator
{
    public Task<string> GenerateAsync(Lead lead, CmaAnalysis analysis, List<Comp> comps, AccountConfig agent, ReportType reportType, byte[]? logoBytes, byte[]? headshotBytes, CancellationToken ct) =>
        Task.FromResult("/tmp/no-op.pdf");
}

file sealed class NoOpCmaNotifier : ICmaNotifier
{
    public Task NotifySellerAsync(string agentId, Lead lead, string pdfPath, CmaAnalysis analysis, string correlationId, CancellationToken ct) =>
        Task.CompletedTask;
}
