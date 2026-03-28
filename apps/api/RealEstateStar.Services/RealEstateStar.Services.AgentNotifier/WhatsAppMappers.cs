namespace RealEstateStar.Services.AgentNotifier;

public static class WhatsAppMappers
{
    public static List<(string type, string value)> ToNewLeadParams(
        string leadName, string phone, string email, string interest, string area)
    {
        return
        [
            ("text", Sanitize(leadName)),
            ("text", Sanitize(phone)),
            ("text", Sanitize(email)),
            ("text", Sanitize(interest)),
            ("text", Sanitize(area))
        ];
    }

    public static List<(string type, string value)> ToCmaReadyParams(
        string leadName, string address, string estimatedValue)
    {
        return
        [
            ("text", Sanitize(leadName)),
            ("text", Sanitize(address)),
            ("text", Sanitize(estimatedValue))
        ];
    }

    public static List<(string type, string value)> ToFollowUpParams(
        string leadName, int daysSinceSubmission)
    {
        return
        [
            ("text", Sanitize(leadName)),
            ("text", daysSinceSubmission.ToString())
        ];
    }

    public static List<(string type, string value)> ToDataDeletionParams(
        string leadName, DateTime deletionDeadline)
    {
        return
        [
            ("text", Sanitize(leadName)),
            ("text", deletionDeadline.ToString("yyyy-MM-dd"))
        ];
    }

    public static List<(string type, string value)> ToWelcomeParams(string agentFirstName)
    {
        return [("text", Sanitize(agentFirstName))];
    }

    // Prevent template injection — strip {{ and }} from all string values
    private static string Sanitize(string value)
        => value.Replace("{{", "").Replace("}}", "");
}
