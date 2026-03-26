namespace RealEstateStar.Clients.RentCast;

public class RentCastOptions
{
    public string ApiKey { get; init; } = "";
    public string BaseUrl { get; init; } = "https://api.rentcast.io/v1/avm/value";
    public int TimeoutSeconds { get; init; } = 30;
    public int MonthlyLimitWarningPercent { get; init; } = 80;
}
