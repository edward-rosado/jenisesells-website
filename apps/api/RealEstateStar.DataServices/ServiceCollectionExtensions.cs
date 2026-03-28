using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.DataServices.Cache;
using RealEstateStar.DataServices.Cma;
using RealEstateStar.DataServices.Config;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.DataServices.Privacy;
using RealEstateStar.DataServices.Storage;
using RealEstateStar.Domain.Cma.Interfaces;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        string configPath)
    {
        // Account config service — reads per-tenant config from disk
        services.AddSingleton<IAccountConfigService>(sp =>
            new AccountConfigService(configPath, sp.GetRequiredService<ILogger<AccountConfigService>>()));

        // Lead feature services
        services.AddSingleton<ILeadStore, LeadFileStore>();
        services.AddSingleton<IMarketingConsentLog, MarketingConsentLog>();
        services.AddSingleton<ILeadDataDeletion, LeadDataDeletion>();
        services.AddSingleton<IDeletionAuditLog, DeletionAuditLog>();

        services.AddSingleton<ILeadDeadLetterStore>(sp =>
            new LeadDeadLetterStore(
                Path.Combine(environment.ContentRootPath, "data", "dead-letter"),
                sp.GetRequiredService<ILogger<LeadDeadLetterStore>>()));

        // Compliance consent triple-write services
        // IComplianceFileStorageProvider: service-account Drive in prod, local filesystem in dev
        services.AddSingleton<IComplianceFileStorageProvider>(sp =>
            new LocalComplianceStorageProvider(
                configuration["Storage:ComplianceBasePath"] ??
                Path.Combine(environment.ContentRootPath, "data", "compliance")));

        // IComplianceConsentWriter: backed by ComplianceConsentWriter
        services.AddSingleton<IComplianceConsentWriter, ComplianceConsentWriter>();

        // IConsentAuditService: Azure Table in prod, no-op in dev
        services.AddSingleton<IConsentAuditService>(sp =>
        {
            var connStr = configuration["AzureStorage:ConnectionString"];
            if (string.IsNullOrEmpty(connStr))
                return new NullConsentAuditService();

            var tableClient = new TableClient(connStr, "consentaudit");
            return new ConsentAuditService(tableClient, sp.GetRequiredService<ILogger<ConsentAuditService>>());
        });

        // GDPR data export
        services.AddSingleton<ILeadDataExport, LeadDataExport>();

        // Consent token store (Azure Table; in-memory fallback when not configured)
        services.AddSingleton<ConsentTokenStore>(sp =>
        {
            var connStr = configuration["AzureStorage:ConnectionString"];
            if (string.IsNullOrEmpty(connStr))
                return new ConsentTokenStore(
                    new TableClient("UseDevelopmentStorage=true", "consenttokens"),
                    sp.GetRequiredService<ILogger<ConsentTokenStore>>());

            var tableClient = new TableClient(connStr, "consenttokens");
            return new ConsentTokenStore(tableClient, sp.GetRequiredService<ILogger<ConsentTokenStore>>());
        });

        // OAuth token store — default to NullTokenStore (no-persist) when connection string is absent.
        // NOTE: Program.cs (Api composition root) overrides this registration with AzureTableTokenStore
        // when AzureStorage:ConnectionString is configured. AzureTableTokenStore lives in Clients.Azure,
        // which DataServices cannot reference per architecture rules. The override registration in
        // Program.cs calls AddSingleton<ITokenStore> after AddDataServices(), replacing this default.
        services.AddSingleton<ITokenStore>(sp =>
        {
            sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(ServiceCollectionExtensions)).LogWarning(
                "[STARTUP-070] AzureStorage:ConnectionString not configured — using NullTokenStore. " +
                "OAuth tokens will NOT be persisted. Set AzureStorage:ConnectionString to enable.");
            return new NullTokenStore();
        });

        // Drive change monitor
        services.AddSingleton<DriveChangeMonitor>();

        // Content cache — shared cross-lead dedup for CMA and HomeSearch results
        services.AddMemoryCache();
        services.AddSingleton<IContentCache, MemoryContentCache>();

        // CMA PDF storage service
        services.AddSingleton<IPdfDataService, PdfDataService>();

        return services;
    }
}
