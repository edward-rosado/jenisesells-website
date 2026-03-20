using System.Text.Json.Serialization;

namespace RealEstateStar.Api.Features.WhatsApp;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    NewLead,
    CmaReady,
    FollowUpReminder,
    ListingAlert,
    DataDeletion,
    Welcome
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MessageDirection
{
    Outbound,
    Inbound
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum IntentType
{
    LeadQuestion,
    ActionRequest,
    Acknowledge,
    Help,
    OutOfScope,
    Unknown
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OutOfScopeCategory
{
    GeneralReQuestion,
    LegalFinancial,
    NonReTopic,
    NoLeadData,
    PromptInjection
}
