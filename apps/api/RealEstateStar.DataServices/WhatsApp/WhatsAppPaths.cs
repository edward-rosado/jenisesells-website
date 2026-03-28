using RealEstateStar.Domain.Leads;

namespace RealEstateStar.DataServices.WhatsApp;

public static class WhatsAppPaths
{
    public static string LeadConversation(string leadName) =>
        $"{LeadPaths.LeadFolder(leadName)}/WhatsApp Conversation.md";

    public const string GeneralConversation =
        "Real Estate Star/WhatsApp/General.md";
}
