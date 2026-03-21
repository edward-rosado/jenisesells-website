namespace RealEstateStar.Api.Features.WhatsApp.Services;

public interface IWhatsAppClient
{
    Task<string> SendTemplateAsync(string toPhoneNumber, string templateName,
        List<(string type, string value)> parameters, CancellationToken ct);
    Task<string> SendFreeformAsync(string toPhoneNumber, string text, CancellationToken ct);
    Task MarkReadAsync(string messageId, CancellationToken ct);
}

public class WhatsAppNotRegisteredException(string phoneNumber) : Exception($"Recipient {phoneNumber} not on WhatsApp")
{
    public string PhoneNumber { get; } = phoneNumber;
}

public class WhatsAppApiException(int code, string message)
    : Exception($"WhatsApp API error {code}: {message}")
{
    public int Code { get; } = code;
}

public class WhatsAppRateLimitException(int code, string message)
    : WhatsAppApiException(code, message);
