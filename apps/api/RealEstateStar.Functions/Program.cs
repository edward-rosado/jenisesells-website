using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSerilog();

// Shared DI registrations will be added here as functions are built in Phases 1-3.
// For now this is an empty Functions host that proves the project structure works.

builder.Build().Run();
