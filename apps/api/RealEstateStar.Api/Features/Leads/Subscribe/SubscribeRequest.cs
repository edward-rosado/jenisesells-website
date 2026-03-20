namespace RealEstateStar.Api.Features.Leads.Subscribe;

public class SubscribeRequest
{
    public required string Email { get; init; }
    public required string Token { get; init; }
}
