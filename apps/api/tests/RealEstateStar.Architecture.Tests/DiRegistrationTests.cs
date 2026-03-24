using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.Shared.Interfaces.External;
using RealEstateStar.Domain.Shared.Interfaces.Storage;
using RealEstateStar.Domain.Leads.Interfaces;
using RealEstateStar.Domain.Notifications.Interfaces;
using RealEstateStar.Domain.Privacy.Interfaces;

namespace RealEstateStar.Architecture.Tests;

/// <summary>
/// Verifies that all Domain interfaces have concrete implementations registered in DI.
/// Catches the class of bug where a new service is created but never wired in Program.cs.
/// </summary>
public class DiRegistrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly IServiceProvider _services;

    public DiRegistrationTests(WebApplicationFactory<Program> factory)
    {
        // Build the service provider without starting the host (avoids needing real connections).
        // Override config to prevent startup failures from missing secrets.
        var app = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((ctx, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Google:ClientId"] = "test",
                    ["Google:ClientSecret"] = "test",
                    ["Google:RedirectUri"] = "http://localhost/oauth/callback",
                    ["Anthropic:ApiKey"] = "test",
                    ["Hmac:HmacSecret"] = "test-secret-at-least-32-characters-long!!",
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                });
            });
        });

        _services = app.Services;
    }

    /// <summary>
    /// Every Domain interface that has a concrete implementation should resolve from DI.
    /// If this test fails, a service was created but never registered in Program.cs.
    /// </summary>
    [Theory]
    [InlineData(typeof(IFileStorageProvider))]
    [InlineData(typeof(IDocumentStorageProvider))]
    [InlineData(typeof(ISheetStorageProvider))]
    [InlineData(typeof(ITokenStore))]
    [InlineData(typeof(IGmailSender))]
    [InlineData(typeof(IGDriveClient))]
    [InlineData(typeof(IGDocsClient))]
    [InlineData(typeof(IGSheetsClient))]
    [InlineData(typeof(IOAuthRefresher))]
    [InlineData(typeof(IAnthropicClient))]
    [InlineData(typeof(IGwsService))]
    [InlineData(typeof(ILeadStore))]
    [InlineData(typeof(ILeadNotifier))]
    [InlineData(typeof(ILeadDataDeletion))]
    [InlineData(typeof(IMarketingConsentLog))]
    public void Domain_interface_resolves_from_DI(Type interfaceType)
    {
        var service = _services.GetService(interfaceType);

        Assert.NotNull(service);
    }

    [Fact]
    public void IFileStorageProvider_is_FanOutStorageProvider()
    {
        var service = _services.GetRequiredService<IFileStorageProvider>();

        // FanOutStorageProvider is the production implementation — not LocalStorageProvider
        Assert.Equal("FanOutStorageProvider", service.GetType().Name);
    }

    [Fact]
    public void IDocumentStorageProvider_forwards_to_IFileStorageProvider()
    {
        var fileProvider = _services.GetRequiredService<IFileStorageProvider>();
        var docProvider = _services.GetRequiredService<IDocumentStorageProvider>();

        Assert.Same(fileProvider, docProvider);
    }

    [Fact]
    public void ISheetStorageProvider_forwards_to_IFileStorageProvider()
    {
        var fileProvider = _services.GetRequiredService<IFileStorageProvider>();
        var sheetProvider = _services.GetRequiredService<ISheetStorageProvider>();

        Assert.Same(fileProvider, sheetProvider);
    }
}
