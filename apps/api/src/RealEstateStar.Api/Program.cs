using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.WithProperty("Application", "RealEstateStar.Api")
        .WriteTo.Console();
});

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Health checks
builder.Services.AddHealthChecks();

// CORS — allow agent site subdomains
builder.Services.AddCors(options =>
{
    options.AddPolicy("AgentSites", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            var uri = new Uri(origin);
            return uri.Host == "localhost"
                || uri.Host.EndsWith(".realestatestar.com");
        })
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors("AgentSites");

// Swagger UI (dev only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health checks
app.MapHealthChecks("/healthz");

// Root endpoint
app.MapGet("/", () => Results.Ok(new
{
    service = "Real Estate Star API",
    status = "running",
    version = "0.1.0"
}));

app.Run();

// Make Program accessible for WebApplicationFactory in tests
public partial class Program { }
