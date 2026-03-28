using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RealEstateStar.Clients.Anthropic;
using RealEstateStar.Clients.Azure;
using RealEstateStar.Clients.GDocs;
using RealEstateStar.Clients.GDrive;
using RealEstateStar.Clients.Gmail;
using RealEstateStar.Clients.GSheets;
using RealEstateStar.Clients.RentCast;
using RealEstateStar.Clients.Scraper;
using RealEstateStar.DataServices;
using RealEstateStar.Workers.Lead.CMA;
using RealEstateStar.Workers.Lead.HomeSearch;
using RealEstateStar.Workers.Lead.Orchestrator;
using RealEstateStar.Workers.WhatsApp;

namespace RealEstateStar.Api.Diagnostics;

public static class OpenTelemetryExtensions
{
    private const string ServiceName = "real-estate-star-api";
    private const string OnboardingSourceName = "RealEstateStar.Onboarding";
    private const string DefaultOtlpEndpoint = "http://localhost:4317";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var rawEndpoint = builder.Configuration["Otel:Endpoint"] ?? DefaultOtlpEndpoint;
        var otlpHeaders = builder.Configuration["Otel:Headers"] ?? "";
        var useHttpProtobuf = !string.IsNullOrEmpty(otlpHeaders);

        // Ensure trailing slash so Uri combination appends paths correctly
        // Without it: new Uri("https://host/otlp", "v1/traces") → /v1/traces (wrong)
        // With it:    new Uri("https://host/otlp/", "v1/traces") → /otlp/v1/traces (correct)
        var otlpBase = new Uri(rawEndpoint.TrimEnd('/') + "/");
        var tracesEndpoint = useHttpProtobuf ? new Uri(otlpBase, "v1/traces") : otlpBase;
        var metricsEndpoint = useHttpProtobuf ? new Uri(otlpBase, "v1/metrics") : otlpBase;

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
                .AddSource(RentCastDiagnostics.ServiceName)
                .AddSource(OrchestratorDiagnostics.ServiceName)
                .AddSource("RealEstateStar.Pdf")
                .AddSource("RealEstateStar.LeadCommunicator")
                .AddSource("RealEstateStar.AgentNotifier")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = tracesEndpoint;
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
                .AddMeter(RentCastDiagnostics.ServiceName)
                .AddMeter(OrchestratorDiagnostics.ServiceName)
                .AddMeter("RealEstateStar.Pdf")
                .AddMeter("RealEstateStar.LeadCommunicator")
                .AddMeter("RealEstateStar.AgentNotifier")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = metricsEndpoint;
                    options.Protocol = useHttpProtobuf ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
                    if (!string.IsNullOrEmpty(otlpHeaders))
                        options.Headers = otlpHeaders;
                }));

        return builder;
    }
}
