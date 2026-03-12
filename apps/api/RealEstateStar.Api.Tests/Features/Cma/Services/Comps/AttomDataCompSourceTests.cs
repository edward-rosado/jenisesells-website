using System.Net;
using FluentAssertions;
using Moq;
using RealEstateStar.Api.Features.Cma;
using RealEstateStar.Api.Features.Cma.Services.Comps;

namespace RealEstateStar.Api.Tests.Features.Cma.Services.Comps;

public class AttomDataCompSourceTests
{
    private static readonly CompSearchRequest DefaultRequest = new()
    {
        Address = "123 Main St", City = "Springfield", State = "NJ", Zip = "07081",
        Beds = 3, Baths = 2, SqFt = 1500
    };

    private static (AttomDataCompSource source, FakeHttpMessageHandler handler) CreateSource(HttpResponseMessage response)
    {
        var handler = new FakeHttpMessageHandler(response);
        var client = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);
        var source = new AttomDataCompSource(factory.Object, "test-api-key");
        return (source, handler);
    }

    [Fact]
    public void Name_ReturnsAttomData()
    {
        var (source, _) = CreateSource(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        source.Name.Should().Be("ATTOM Data");
    }

    [Fact]
    public async Task FetchAsync_MakesHttpRequest_AndReturnsResults()
    {
        var (source, handler) = CreateSource(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}")
        });

        var result = await source.FetchAsync(DefaultRequest, CancellationToken.None);

        result.Should().NotBeNull();
        handler.RequestMade.Should().BeTrue();
        handler.LastRequest!.RequestUri!.ToString().Should().Contain("attomdata.com");
        handler.LastRequest.Headers.GetValues("apikey").Should().Contain("test-api-key");
    }

    [Fact]
    public async Task FetchAsync_ThrowsOnHttpError()
    {
        var (source, _) = CreateSource(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = () => source.FetchAsync(DefaultRequest, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
