namespace RealEstateStar.Clients.GooglePlaces;

public class GooglePlacesOptions
{
    /// <summary>Google Cloud API key with Places API (New) enabled.</summary>
    public string ApiKey { get; init; } = "";

    /// <summary>HTTP request timeout in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 15;
}
