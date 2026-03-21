using System.Text.Json.Serialization;

namespace RealEstateStar.Domain.Privacy;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ConsentSource { LeadForm, PrivacyPage, EmailLink, Api }
