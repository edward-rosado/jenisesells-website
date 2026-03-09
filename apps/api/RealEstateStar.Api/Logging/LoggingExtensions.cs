using Serilog;

namespace RealEstateStar.Api.Logging;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddStructuredLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, config) =>
        {
            config
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "RealEstateStar.Api")
                .WriteTo.Console(outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

            var otlpEndpoint = context.Configuration["Otel:Endpoint"];
            if (!string.IsNullOrEmpty(otlpEndpoint))
                config.WriteTo.OpenTelemetry(opts => opts.Endpoint = otlpEndpoint);
        });
        return builder;
    }
}
