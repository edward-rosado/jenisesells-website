namespace RealEstateStar.Api.Features.Leads.OptOut;

public class OptOutRequest
{
    public required string Email { get; init; }
    public required string Token { get; init; }
}
