namespace RealEstateStar.Clients.Zillow;

public class ZillowOptions
{
    /// <summary>Bridge Interactive API server token (Bearer auth).</summary>
    public string ApiToken { get; init; } = "";

    /// <summary>Bridge Interactive OData base URL.</summary>
    public string BaseUrl { get; init; } = "https://api.bridgedataoutput.com/api/v2/OData/reviews";

    /// <summary>HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 15;
}
