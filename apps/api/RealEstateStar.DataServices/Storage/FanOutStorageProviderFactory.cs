using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;

namespace RealEstateStar.DataServices.Storage;

/// <summary>
/// Creates per-agent FanOutStorageProvider instances that write to the agent's Google Drive.
/// All heavy dependencies (IGDriveClient, IGSheetsClient, etc.) are shared singletons —
/// the factory-created instance only holds lightweight string context (accountId, agentId).
/// Instances are short-lived: created at the start of a persist operation and GC'd when done.
/// </summary>
public sealed class FanOutStorageProviderFactory(IServiceProvider sp) : IFileStorageProviderFactory
{
    public IFileStorageProvider CreateForAgent(string accountId, string agentId)
    {
        var platformStore =
            sp.GetKeyedService<IDocumentStorageProvider>(StorageServiceCollectionExtensions.PlatformDocumentStoreKey)
            ?? throw new InvalidOperationException("Platform document store not registered — ensure AzureStorage:ConnectionString is configured");

        return new FanOutStorageProvider(
            sp.GetRequiredService<IGDriveClient>(),
            sp.GetRequiredService<IGSheetsClient>(),
            sp.GetRequiredService<IGwsService>(),
            platformStore,
            accountId,
            agentId,
            "",
            sp.GetRequiredService<ILogger<FanOutStorageProvider>>());
    }
}
