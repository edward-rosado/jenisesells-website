using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.WhatsApp.Interfaces;

namespace RealEstateStar.Workers.WhatsApp;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the WhatsApp conversation pipeline services owned by this project:
    /// intent classifier, response generator, and conversation handler stubs.
    /// The conditional Azure queue/table services, IWhatsAppSender, IWhatsAppNotifier,
    /// IEmailNotifier, IConversationLogger, and the "WhatsApp" HttpClient resilience
    /// pipeline are registered by the Api composition root (Program.cs), as they
    /// depend on projects outside the Workers.* → Domain + Workers.Shared constraint.
    /// When WhatsApp is enabled (PhoneNumberId configured), the hosted services
    /// WebhookProcessorWorker and WhatsAppRetryJob are also registered by the Api layer.
    /// </summary>
    public static IServiceCollection AddWhatsAppWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IIntentClassifier, NoopIntentClassifier>();
        services.AddSingleton<IResponseGenerator, NoopResponseGenerator>();
        services.AddSingleton<IConversationHandler, ConversationHandler>();

        var phoneNumberId = configuration["WhatsApp:PhoneNumberId"];
        if (!string.IsNullOrEmpty(phoneNumberId))
        {
            services.AddHostedService<WebhookProcessorWorker>();
            services.AddHostedService<WhatsAppRetryJob>();
        }

        return services;
    }
}
