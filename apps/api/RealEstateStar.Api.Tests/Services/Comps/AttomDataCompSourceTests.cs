using System.Net;
using FluentAssertions;
using RealEstateStar.Api.Services.Comps;

namespace RealEstateStar.Api.Tests.Services.Comps;

public class AttomDataCompSourceTests
{
    [Fact]
    public void Name_ReturnsAttomData()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });
        var client = new HttpClient(handler);
        var source = new AttomDataCompSource(client, "test-api-key");

        source.Name.Should().Be("ATTOM Data");
    }

    [Fact]
    public async Task FetchAsync_MakesHttpRequest_AndReturnsResults()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });
        var client = new HttpClient(handler);
        var source = new AttomDataCompSource(client, "test-api-key");

        var result = await source.FetchAsync("123 Main St", "Springfield", "NJ", "07081", 3, 2, 1500, CancellationToken.None);

        result.Should().NotBeNull();
        handler.RequestMade.Should().BeTrue();
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("attomdata.com");
        handler.LastRequest.Headers.GetValues("apikey").Should().Contain("test-api-key");
    }

    [Fact]
    public async Task FetchAsync_ThrowsOnHttpError()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = new HttpClient(handler);
        var source = new AttomDataCompSource(client, "test-api-key");

        var act = () => source.FetchAsync("123 Main St", "Springfield", "NJ", "07081", 3, 2, 1500, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
