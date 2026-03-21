using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RealEstateStar.Api.Features.WhatsApp.Services;

public class WhatsAppClient(
    IHttpClientFactory httpClientFactory,
    string phoneNumberId,
    string accessToken,
    ILogger<WhatsAppClient>? logger = null) : IWhatsAppSender
{
    public async Task<string> SendTemplateAsync(string toPhoneNumber, string templateName,
        List<(string type, string value)> parameters, CancellationToken ct)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = "en_US" },
                components = new[]
                {
                    new
                    {
                        type = "body",
                        parameters = parameters.Select(p => new { type = p.type, text = p.value }).ToArray()
                    }
                }
            }
        };
        return await SendAsync(payload, toPhoneNumber, ct);
    }

    public async Task<string> SendFreeformAsync(string toPhoneNumber, string text, CancellationToken ct)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = toPhoneNumber,
            type = "text",
            text = new { body = text }
        };
        return await SendAsync(payload, toPhoneNumber, ct);
    }

    public async Task MarkReadAsync(string messageId, CancellationToken ct)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = messageId
        };

        var client = httpClientFactory.CreateClient("WhatsApp");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{phoneNumberId}/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            logger?.LogWarning("[WA-013] Failed to mark message {MessageId} as read", messageId);
    }

    private async Task<string> SendAsync(object payload, string toPhoneNumber, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("WhatsApp");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{phoneNumberId}/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonDocument.Parse(body);
            var code = error.RootElement.GetProperty("error").GetProperty("code").GetInt32();
            var message = error.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "";

            if (code == 131026)
                throw new WhatsAppNotRegisteredException(toPhoneNumber);

            if ((int)response.StatusCode == 429)
                throw new WhatsAppRateLimitException(code, message);

            throw new WhatsAppApiException(code, message);
        }

        var result = JsonDocument.Parse(body);
        return result.RootElement.GetProperty("messages")[0].GetProperty("id").GetString() ?? "";
    }
}
