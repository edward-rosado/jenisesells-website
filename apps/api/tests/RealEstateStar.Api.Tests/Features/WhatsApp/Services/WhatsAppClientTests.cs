using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealEstateStar.DataServices.WhatsApp;
using RealEstateStar.Api.Tests.TestHelpers;

namespace RealEstateStar.Api.Tests.Features.WhatsApp.Services;

public class WhatsAppClientTests
{
    private readonly Mock<IHttpClientFactory> _httpFactory = new();
    private readonly MockHttpMessageHandler _handler = new();
    private readonly WhatsAppClient _sut;

    public WhatsAppClientTests()
    {
        var httpClient = new HttpClient(_handler)
        {
            BaseAddress = new Uri("https://graph.facebook.com/v20.0/")
        };
        _httpFactory.Setup(f => f.CreateClient("WhatsApp")).Returns(httpClient);
        _sut = new WhatsAppClient(_httpFactory.Object, "PHONE_ID", "ACCESS_TOKEN",
            Mock.Of<ILogger<WhatsAppClient>>());
    }

    [Fact]
    public async Task SendTemplateAsync_PostsCorrectPayload()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"messaging_product":"whatsapp","messages":[{"id":"wamid.abc"}]}""")
        };

        var result = await _sut.SendTemplateAsync("+12015551234", "new_lead_notification",
            [("text", "Jane Smith"), ("text", "+1 201 555 9876")], CancellationToken.None);

        result.Should().Be("wamid.abc");
        _handler.LastRequest!.RequestUri!.PathAndQuery.Should().Contain("PHONE_ID/messages");
        var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
        body.Should().Contain("new_lead_notification");
        body.Should().Contain("Jane Smith");
    }

    [Fact]
    public async Task SendFreeformAsync_PostsTextMessage()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"messaging_product":"whatsapp","messages":[{"id":"wamid.def"}]}""")
        };

        var result = await _sut.SendFreeformAsync("+12015551234",
            "Jane works at Deloitte.", CancellationToken.None);

        result.Should().Be("wamid.def");
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("Jane works at Deloitte.");
        body.Should().Contain("\"type\":\"text\"");
    }

    [Fact]
    public async Task MarkReadAsync_PostsCorrectPayload()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"success":true}""")
        };

        await _sut.MarkReadAsync("wamid.abc123", CancellationToken.None);

        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        body.Should().Contain("\"status\":\"read\"");
        body.Should().Contain("wamid.abc123");
    }

    [Fact]
    public async Task SendTemplateAsync_Throws_On131026_NotRegistered()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":{"code":131026,"message":"Recipient not on WhatsApp"}}""")
        };

        var act = () => _sut.SendTemplateAsync("+12015551234", "welcome_onboarding",
            [("text", "Jenise")], CancellationToken.None);
        var ex = await act.Should().ThrowAsync<WhatsAppNotRegisteredException>();
        ex.Which.PhoneNumber.Should().Be("+12015551234");
    }

    [Fact]
    public async Task SendTemplateAsync_Throws_OnOtherApiError()
    {
        _handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error":{"code":500,"message":"Internal error"}}""")
        };

        var act = () => _sut.SendTemplateAsync("+12015551234", "new_lead_notification",
            [("text", "Jane")], CancellationToken.None);
        await act.Should().ThrowAsync<WhatsAppApiException>();
    }
}
