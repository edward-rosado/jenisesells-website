namespace RealEstateStar.Api.Features.Onboarding.Tools;

public class CloudflareOptions
{
    public string ApiToken { get; init; } = "";
    public string AccountId { get; init; } = "";

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(ApiToken) &&
        !string.IsNullOrWhiteSpace(AccountId);
}
