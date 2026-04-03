using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RealEstateStar.Domain.WhatsApp.Interfaces;

namespace RealEstateStar.Workers.WhatsApp;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers WhatsApp conversation pipeline services: intent classifier, response generator,
    /// conversation handler stubs, and <c>WhatsAppRetryJob</c>.
    /// The conditional <c>WebhookProcessorWorker</c> and <c>WhatsAppRetryJob</c> hosted services
    /// were removed in Phase 4; Azure Durable Functions queue/timer triggers now handle that role.
    /// The Azure queue/table, IWhatsAppSender, IWhatsAppNotifier, IEmailNotifier, IConversationLogger,
    /// and the "WhatsApp" HttpClient resilience pipeline are registered by the Api composition root
    /// (Program.cs), as they depend on projects outside the Workers.* → Domain + Workers.Shared constraint.
    /// </summary>
    public static IServiceCollection AddWhatsAppWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IIntentClassifier, NoopIntentClassifier>();
        services.AddSingleton<IResponseGenerator, NoopResponseGenerator>();
        services.AddSingleton<IConversationHandler, ConversationHandler>();

        // Always register WhatsAppRetryJob so it can be resolved by WhatsAppRetryFunction
        // (timer-triggered Azure Function).
        services.AddSingleton<WhatsAppRetryJob>();

        return services;
    }
}
