using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RealEstateStar.Clients.Scraper;
using RealEstateStar.Domain.WhatsApp;
using RealEstateStar.Domain.Shared;

namespace RealEstateStar.Api.Diagnostics;

public static class OpenTelemetryExtensions
{
    private const string ServiceName = "real-estate-star-api";
    private const string OnboardingSourceName = "RealEstateStar.Onboarding";
    private const string DefaultOtlpEndpoint = "http://localhost:4317";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var otlpEndpoint = new Uri(
            builder.Configuration["Otel:Endpoint"] ?? DefaultOtlpEndpoint);
        var otlpHeaders = builder.Configuration["Otel:Headers"] ?? "";
        var useHttpProtobuf = !string.IsNullOrEmpty(otlpHeaders);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName))
            .WithTracing(tracing => tracing
                .AddSource(OnboardingSourceName)
                .AddSource(LeadDiagnostics.ServiceName)
                .AddSource(CmaDiagnostics.ServiceName)
                .AddSource(HomeSearchDiagnostics.ServiceName)
                .AddSource(ScraperDiagnostics.ServiceName)
                .AddSource(WhatsAppDiagnostics.ServiceName)
                .AddSource(ClaudeDiagnostics.ServiceName)
                .AddSource(GmailDiagnostics.ServiceName)
                .AddSource(GDriveDiagnostics.ServiceName)
                .AddSource(GDocsDiagnostics.ServiceName)
                .AddSource(GSheetsDiagnostics.ServiceName)
                .AddSource(TokenStoreDiagnostics.ServiceName)
                .AddSource(FanOutDiagnostics.ServiceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = useHttpProtobuf ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
                    if (!string.IsNullOrEmpty(otlpHeaders))
                        options.Headers = otlpHeaders;
                }))
            .WithMetrics(metrics => metrics
                .AddMeter(OnboardingSourceName)
                .AddMeter(LeadDiagnostics.ServiceName)
                .AddMeter(CmaDiagnostics.ServiceName)
                .AddMeter(HomeSearchDiagnostics.ServiceName)
                .AddMeter(ScraperDiagnostics.ServiceName)
                .AddMeter(WhatsAppDiagnostics.ServiceName)
                .AddMeter(ClaudeDiagnostics.ServiceName)
                .AddMeter(GmailDiagnostics.ServiceName)
                .AddMeter(GDriveDiagnostics.ServiceName)
                .AddMeter(GDocsDiagnostics.ServiceName)
                .AddMeter(GSheetsDiagnostics.ServiceName)
                .AddMeter(TokenStoreDiagnostics.ServiceName)
                .AddMeter(FanOutDiagnostics.ServiceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = otlpEndpoint;
                    options.Protocol = useHttpProtobuf ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
                    if (!string.IsNullOrEmpty(otlpHeaders))
                        options.Headers = otlpHeaders;
                }));

        return builder;
    }
}
