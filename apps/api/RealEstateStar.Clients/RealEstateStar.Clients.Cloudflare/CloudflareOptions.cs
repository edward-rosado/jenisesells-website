namespace RealEstateStar.Clients.Cloudflare;

public sealed class CloudflareOptions
{
    public string ApiToken { get; init; } = "";
    public string AccountId { get; init; } = "";
    public string ZoneId { get; init; } = "";
}
