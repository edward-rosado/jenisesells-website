using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Cloudflare;

public static class ServiceCollectionExtensions
{
    private const string BaseUrl = "https://api.cloudflare.com/client/v4/accounts/";
    private const string BaseUrlRoot = "https://api.cloudflare.com/client/v4/";

    public static IServiceCollection AddCloudflareClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CloudflareOptions>(configuration.GetSection("Cloudflare"));

        // KV client — base URL includes account ID
        services.AddHttpClient<ICloudflareKvClient, CloudflareKvClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<CloudflareOptions>>().Value;
            client.BaseAddress = new Uri($"{BaseUrl}{opts.AccountId}/storage/kv/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiToken);
        });

        // R2 client — base URL includes account ID
        services.AddHttpClient<ICloudflareR2Client, CloudflareR2Client>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<CloudflareOptions>>().Value;
            client.BaseAddress = new Uri($"{BaseUrl}{opts.AccountId}/r2/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiToken);
        });

        // ForSaaS client — registered as factory so we can inject zoneId from options
        services.AddSingleton<ICloudflareForSaasClient>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<CloudflareOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<CloudflareForSaasClient>>();
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("CloudflareForSaaS");
            return new CloudflareForSaasClient(httpClient, opts.ZoneId, logger);
        });

        services.AddHttpClient("CloudflareForSaaS", (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<CloudflareOptions>>().Value;
            client.BaseAddress = new Uri(BaseUrlRoot);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.ApiToken);
        });

        return services;
    }
}
