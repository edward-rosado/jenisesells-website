using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RealEstateStar.DataServices.Leads;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Storage;

public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Service key used for the platform-tier document store keyed registration.
    /// The Api composition root (Program.cs) registers AzureBlobDocumentStore under this key
    /// when AzureStorage:ConnectionString is configured; this extension resolves it at runtime.
    /// Using a keyed service avoids a compile-time dependency on Clients.Azure from DataServices.
    /// </summary>
    public const string PlatformDocumentStoreKey = "platform-blob";

    /// <summary>
    /// Registers the FanOutStorageProvider as IFileStorageProvider and forwards the narrower
    /// IDocumentStorageProvider and ISheetStorageProvider interfaces to it.
    ///
    /// Platform-tier document storage is resolved via a keyed IDocumentStorageProvider with key
    /// <see cref="PlatformDocumentStoreKey"/>. Register AzureBlobDocumentStore (from Clients.Azure)
    /// under that key in Program.cs before calling this method. When the keyed service is absent,
    /// falls back to LocalStorageProvider using Storage:BasePath config or the host content root.
    /// </summary>
    public static IServiceCollection AddStorageProviders(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // FanOutStorageProvider: three-tier fan-out (Agent Drive + Account Drive + Platform Blob/Local).
        // Registered as IFileStorageProvider so all lead storage flows through fan-out.
        services.AddSingleton<IFileStorageProvider>(sp =>
        {
            // Platform tier: prefer keyed IDocumentStorageProvider registered by the Api composition
            // root (e.g. AzureBlobDocumentStore from Clients.Azure), fall back to LocalStorageProvider.
            var platformStore =
                sp.GetKeyedService<IDocumentStorageProvider>(PlatformDocumentStoreKey)
                ?? (IDocumentStorageProvider)new LocalStorageProvider(
                    configuration["Storage:BasePath"] ?? Path.Combine(environment.ContentRootPath, "data"));

            return new FanOutStorageProvider(
                sp.GetRequiredService<IGDriveClient>(),
                sp.GetRequiredService<IGSheetsClient>(),
                sp.GetRequiredService<IGwsService>(),
                platformStore,
                "platform",  // accountId — platform-level, not per-agent
                "platform",  // agentId — platform-level
                "",          // platformEmail — no longer needed for document ops, only sheets
                sp.GetRequiredService<ILogger<FanOutStorageProvider>>());
        });

        // Forward the narrower interfaces to FanOutStorageProvider
        services.AddSingleton<IDocumentStorageProvider>(sp => sp.GetRequiredService<IFileStorageProvider>());
        services.AddSingleton<ISheetStorageProvider>(sp => sp.GetRequiredService<IFileStorageProvider>());

        return services;
    }
}
