using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RealEstateStar.Domain.Shared.Interfaces.External;

namespace RealEstateStar.Clients.Cloudflare;

public static class ServiceCollectionExtensions
{
    private const string BaseUrl = "https://api.cloudflare.com/client/v4/accounts/";

    public static IServiceCollection AddCloudflareKvClient(
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

        return services;
    }
}
