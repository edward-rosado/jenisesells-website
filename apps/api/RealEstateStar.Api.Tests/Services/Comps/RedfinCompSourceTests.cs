using System.Net;
using FluentAssertions;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Services.Comps;

namespace RealEstateStar.Api.Tests.Services.Comps;

public class RedfinCompSourceTests
{
    private static readonly CompSearchRequest DefaultRequest = new()
    {
        Address = "123 Main St", City = "Springfield", State = "NJ", Zip = "07081",
        Beds = 3, Baths = 2, SqFt = 1500
    };

    [Fact]
    public void Name_ReturnsRedfin()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html></html>")
        });
        var client = new HttpClient(handler);
        var source = new RedfinCompSource(client);

        source.Name.Should().Be("Redfin");
    }

    [Fact]
    public async Task FetchAsync_MakesHttpRequest_AndReturnsResults()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>comps data</html>")
        });
        var client = new HttpClient(handler);
        var source = new RedfinCompSource(client);

        var result = await source.FetchAsync(DefaultRequest, CancellationToken.None);

        result.Should().NotBeNull();
        handler.RequestMade.Should().BeTrue();
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("redfin.com");
    }

    [Fact]
    public async Task FetchAsync_ThrowsOnHttpError()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new HttpClient(handler);
        var source = new RedfinCompSource(client);

        var act = () => source.FetchAsync(DefaultRequest, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
