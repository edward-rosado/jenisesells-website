namespace RealEstateStar.TestUtilities;

public class MockHttpMessageHandler : HttpMessageHandler
{
    public HttpResponseMessage ResponseToReturn { get; set; } = new(System.Net.HttpStatusCode.OK);
    public HttpRequestMessage? LastRequest { get; private set; }
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        Requests.Add(request);
        return Task.FromResult(ResponseToReturn);
    }
}
