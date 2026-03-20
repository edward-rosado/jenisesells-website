using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RealEstateStar.Api.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory that injects test configuration values
/// so Program.cs startup validation doesn't throw in CI.
/// Uses UseSetting which adds to the host configuration before builder.Build().
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting injects into the configuration before Program.cs reads it
        builder.UseSetting("Anthropic:ApiKey", "test-anthropic-key");
        builder.UseSetting("Attom:ApiKey", "test-attom-key");
        builder.UseSetting("Google:ClientId", "test-google-client-id");
        builder.UseSetting("Google:ClientSecret", "test-google-client-secret");
        builder.UseSetting("Google:RedirectUri", "http://localhost:5000/oauth/google/callback");
        builder.UseSetting("Stripe:SecretKey", "sk_test_placeholder");
        builder.UseSetting("Stripe:PriceId", "price_test_placeholder");
        builder.UseSetting("Stripe:WebhookSecret", "whsec_test_placeholder");
        builder.UseSetting("Platform:BaseUrl", "http://localhost:3000");

        // WhatsApp — intentionally left empty so the disabled-path (noop) is exercised.
        // Set WhatsApp:PhoneNumberId to a non-empty value if you need to test the
        // enabled path — also set AzureStorage:ConnectionString in that case.
        // builder.UseSetting("WhatsApp:PhoneNumberId", "");
    }
}
