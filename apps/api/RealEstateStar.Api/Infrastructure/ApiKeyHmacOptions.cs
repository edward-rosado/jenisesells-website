namespace RealEstateStar.Api.Infrastructure;

public class ApiKeyHmacOptions
{
    public Dictionary<string, string> ApiKeys { get; set; } = new(); // apiKey → agentId
    public string HmacSecret { get; set; } = "";
    public int MaxTimestampDriftSeconds { get; set; } = 300; // 5 minutes
}
