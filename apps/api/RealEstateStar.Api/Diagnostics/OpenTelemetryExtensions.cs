using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = OtlpExportProtocol.Grpc;
                }))
            .WithMetrics(metrics => metrics
                .AddMeter(OnboardingSourceName)
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
