using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using RealEstateStar.Api.Infrastructure;

namespace RealEstateStar.Api.Features.WhatsApp.Webhook.VerifyWebhook;

public class VerifyWebhookEndpoint : IEndpoint
{
    public void MapEndpoint(WebApplication app) =>
        app.MapGet("/webhooks/whatsapp", (
            [FromQuery(Name = "hub.mode")] string mode,
            [FromQuery(Name = "hub.verify_token")] string verifyToken,
            [FromQuery(Name = "hub.challenge")] string challenge,
            [FromServices] IConfiguration config) =>
            Handle(mode, verifyToken, challenge, config["WhatsApp:VerifyToken"] ?? ""))
            .DisableRateLimiting();

    internal static IResult Handle(string mode, string verifyToken, string challenge, string expectedToken)
    {
        if (mode != "subscribe" ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(verifyToken),
                Encoding.UTF8.GetBytes(expectedToken)))
            return Results.Forbid();

        return Results.Text(challenge);
    }
}
