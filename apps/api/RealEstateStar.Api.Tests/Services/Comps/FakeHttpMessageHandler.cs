using System.Net;

namespace RealEstateStar.Api.Tests.Services.Comps;

public class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpResponseMessage _response;

    public bool RequestMade { get; private set; }
    public HttpRequestMessage? LastRequest { get; private set; }

    public FakeHttpMessageHandler(HttpResponseMessage response)
    {
        _response = response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestMade = true;
        LastRequest = request;
        return Task.FromResult(_response);
    }
}
