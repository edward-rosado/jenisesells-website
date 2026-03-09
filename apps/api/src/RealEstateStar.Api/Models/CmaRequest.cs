using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SellTimeline { Asap, OneToThreeMonths, ThreeToSixMonths, SixToTwelveMonths, Curious }

/// <summary>
/// Mirrors the FormData fields from CmaForm.tsx.
/// Received as application/x-www-form-urlencoded from the agent site.
/// </summary>
public sealed record CmaRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Phone { get; init; }
    public required string Address { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string Zip { get; init; }
    public required string Timeline { get; init; }
    public string? Notes { get; init; }
}
