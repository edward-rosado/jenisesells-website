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
            var otlpHeaders = context.Configuration["Otel:Headers"];
            if (!string.IsNullOrEmpty(otlpEndpoint))
                config.WriteTo.OpenTelemetry(opts =>
                {
                    opts.Endpoint = otlpEndpoint;
                    opts.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.HttpProtobuf;
                    if (!string.IsNullOrEmpty(otlpHeaders))
                    {
                        var parts = otlpHeaders.Split('=', 2);
                        if (parts.Length == 2)
                            opts.Headers = new Dictionary<string, string> { [parts[0]] = parts[1] };
                    }
                    opts.ResourceAttributes = new Dictionary<string, object>
                    {
                        ["service.name"] = "real-estate-star-api"
                    };
                });
        });
        return builder;
    }
}
