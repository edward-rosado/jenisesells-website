using System.Net;
using FluentAssertions;
using RealEstateStar.Api.Services.Comps;

namespace RealEstateStar.Api.Tests.Services.Comps;

public class RedfinCompSourceTests
{
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

        var result = await source.FetchAsync("123 Main St", "Springfield", "NJ", "07081", 3, 2, 1500, CancellationToken.None);

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

        var act = () => source.FetchAsync("123 Main St", "Springfield", "NJ", "07081", 3, 2, 1500, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
