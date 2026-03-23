using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RealEstateStar.Clients.Scraper;

namespace RealEstateStar.Api.Diagnostics;

public static class OpenTelemetryExtensions
{
    private const string ServiceName = "RealEstateStar.Api";
    private const string OnboardingSourceName = "RealEstateStar.Onboarding";
    private const string DefaultOtlpEndpoint = "http://localhost:4317";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var otlpEndpoint = new Uri(
            builder.Configuration["Otel:Endpoint"] ?? DefaultOtlpEndpoint);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddSource(OnboardingSourceName)
                .AddSource(LeadDiagnostics.ServiceName)
                .AddSource(CmaDiagnostics.ServiceName)
                .AddSource(HomeSearchDiagnostics.ServiceName)
                .AddSource(ScraperDiagnostics.ServiceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(metrics => metrics
                .AddMeter(OnboardingSourceName)
                .AddMeter(LeadDiagnostics.ServiceName)
                .AddMeter(CmaDiagnostics.ServiceName)
                .AddMeter(HomeSearchDiagnostics.ServiceName)
                .AddMeter(ScraperDiagnostics.ServiceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpExportProtocol.Grpc;
                }));

        return builder;
    }
}
