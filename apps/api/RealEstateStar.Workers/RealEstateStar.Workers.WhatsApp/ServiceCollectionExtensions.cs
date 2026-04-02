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

        // Always register WhatsAppRetryJob so it can be resolved by WhatsAppRetryFunction
        // (timer-triggered Azure Function) regardless of the BackgroundService feature flag.
        services.AddSingleton<WhatsAppRetryJob>();

        var phoneNumberId = configuration["WhatsApp:PhoneNumberId"];
        if (!string.IsNullOrEmpty(phoneNumberId))
        {
            // Feature flag: Features:WhatsApp:UseBackgroundService (default true during transition).
            // Set to false when Azure Functions (ProcessWebhookFunction, WhatsAppRetryFunction)
            // are fully active to stop the BackgroundService polling loops.
            // Both can be active simultaneously — queue trigger auto-completes messages,
            // preventing duplicates.
            //
            // TODO [Phase 4]: Remove UseBackgroundService flag and these hosted services entirely
            // once Azure Functions are proven stable in production.
            var flagValue = configuration["Features:WhatsApp:UseBackgroundService"];
            var useBackgroundService = !string.Equals(flagValue, "false", StringComparison.OrdinalIgnoreCase);
            if (useBackgroundService)
            {
                services.AddHostedService<WebhookProcessorWorker>();
                services.AddHostedService<WhatsAppRetryJob>();
            }
        }

        return services;
    }
}
