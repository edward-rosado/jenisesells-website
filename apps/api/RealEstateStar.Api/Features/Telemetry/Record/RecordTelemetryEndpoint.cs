using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Infrastructure;
using RealEstateStar.Domain.Leads;

namespace RealEstateStar.Api.Features.Telemetry.Record;

public class RecordTelemetryEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapPost("/telemetry", Handle)
            .RequireRateLimiting("telemetry");

    internal static IResult Handle([FromBody] RecordTelemetryRequest request)
    {
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
        {
            return Results.ValidationProblem(
                validationResults
                    .GroupBy(v => v.MemberNames.FirstOrDefault() ?? "")
                    .ToDictionary(g => g.Key, g => g.Select(v => v.ErrorMessage!).ToArray()));
        }

        // Event is guaranteed non-null by [Required] validation above
        switch (request.Event!.Value)
        {
            case FormEvent.Viewed: LeadDiagnostics.FormViewed.Add(1); break;
            case FormEvent.Started: LeadDiagnostics.FormStarted.Add(1); break;
            case FormEvent.Submitted: LeadDiagnostics.FormSubmitted.Add(1); break;
            case FormEvent.Succeeded: LeadDiagnostics.FormSucceeded.Add(1); break;
            case FormEvent.Failed: LeadDiagnostics.FormFailed.Add(1); break;
        }

        return Results.NoContent();
    }
}
