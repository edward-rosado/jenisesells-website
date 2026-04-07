using System.Text.Json.Serialization;

namespace RealEstateStar.Functions.Activation.Dtos;

// ── Phase 1 Gather ────────────────────────────────────────────────────────────

/// <summary>Input to the EmailFetch activity function.</summary>
public sealed record EmailFetchInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
}

/// <summary>Output from the EmailFetch activity function — the full email corpus.</summary>
public sealed record EmailFetchOutput
{
    [JsonPropertyName("sentEmails")] public List<EmailMessageDto> SentEmails { get; init; } = [];
    [JsonPropertyName("inboxEmails")] public List<EmailMessageDto> InboxEmails { get; init; } = [];
    [JsonPropertyName("signature")] public EmailSignatureDto? Signature { get; init; }
}

public sealed record EmailMessageDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = default!;
    [JsonPropertyName("subject")] public string Subject { get; init; } = default!;
    [JsonPropertyName("body")] public string Body { get; init; } = default!;
    [JsonPropertyName("from")] public string From { get; init; } = default!;
    [JsonPropertyName("to")] public string[] To { get; init; } = default!;
    [JsonPropertyName("date")] public DateTime Date { get; init; }
    [JsonPropertyName("signatureBlock")] public string? SignatureBlock { get; init; }
    [JsonPropertyName("detectedLocale")] public string? DetectedLocale { get; init; }
}

public sealed record EmailSignatureDto
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("phone")] public string? Phone { get; init; }
    [JsonPropertyName("licenseNumber")] public string? LicenseNumber { get; init; }
    [JsonPropertyName("brokerageName")] public string? BrokerageName { get; init; }
    [JsonPropertyName("socialLinks")] public List<string> SocialLinks { get; init; } = [];
    [JsonPropertyName("headshotUrl")] public string? HeadshotUrl { get; init; }
    [JsonPropertyName("websiteUrl")] public string? WebsiteUrl { get; init; }
    [JsonPropertyName("logoUrl")] public string? LogoUrl { get; init; }
}

/// <summary>Input to the DriveIndex activity function.</summary>
public sealed record DriveIndexInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
}

/// <summary>Output from the DriveIndex activity — the indexed drive contents.</summary>
public sealed record DriveIndexOutput
{
    [JsonPropertyName("folderId")] public string FolderId { get; init; } = default!;
    [JsonPropertyName("files")] public List<DriveFileDto> Files { get; init; } = [];
    [JsonPropertyName("contents")] public Dictionary<string, string> Contents { get; init; } = [];
    [JsonPropertyName("discoveredUrls")] public List<string> DiscoveredUrls { get; init; } = [];
    [JsonPropertyName("extractions")] public List<DocumentExtractionDto> Extractions { get; init; } = [];
}

public sealed record DriveFileDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = default!;
    [JsonPropertyName("name")] public string Name { get; init; } = default!;
    [JsonPropertyName("mimeType")] public string MimeType { get; init; } = default!;
    [JsonPropertyName("category")] public string Category { get; init; } = default!;
    [JsonPropertyName("modifiedDate")] public DateTime ModifiedDate { get; init; }
    [JsonPropertyName("detectedLocale")] public string? DetectedLocale { get; init; }
}

public sealed record DocumentExtractionDto
{
    [JsonPropertyName("driveFileId")] public string DriveFileId { get; init; } = default!;
    [JsonPropertyName("fileName")] public string FileName { get; init; } = default!;
    [JsonPropertyName("type")] public string Type { get; init; } = default!;
    [JsonPropertyName("clients")] public List<ExtractedClientDto> Clients { get; init; } = [];
    [JsonPropertyName("property")] public ExtractedPropertyDto? Property { get; init; }
    [JsonPropertyName("date")] public DateTime? Date { get; init; }
    [JsonPropertyName("keyTerms")] public ExtractedKeyTermsDto? KeyTerms { get; init; }
}

public sealed record ExtractedClientDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = default!;
    [JsonPropertyName("role")] public string Role { get; init; } = default!;
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("phone")] public string? Phone { get; init; }
}

public sealed record ExtractedPropertyDto
{
    [JsonPropertyName("address")] public string Address { get; init; } = default!;
    [JsonPropertyName("city")] public string? City { get; init; }
    [JsonPropertyName("state")] public string? State { get; init; }
    [JsonPropertyName("zip")] public string? Zip { get; init; }
}

public sealed record ExtractedKeyTermsDto
{
    [JsonPropertyName("price")] public string? Price { get; init; }
    [JsonPropertyName("commission")] public string? Commission { get; init; }
    [JsonPropertyName("contingencies")] public List<string> Contingencies { get; init; } = [];
}

/// <summary>Input to the AgentDiscovery activity function.</summary>
public sealed record AgentDiscoveryInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
    [JsonPropertyName("agentName")] public string AgentName { get; init; } = default!;
    [JsonPropertyName("emailSignature")] public EmailSignatureDto? EmailSignature { get; init; }
}

/// <summary>Output from the AgentDiscovery activity.</summary>
public sealed record AgentDiscoveryOutput
{
    [JsonPropertyName("headshotBytes")] public byte[]? HeadshotBytes { get; init; }
    [JsonPropertyName("logoBytes")] public byte[]? LogoBytes { get; init; }
    [JsonPropertyName("phone")] public string? Phone { get; init; }
    [JsonPropertyName("websites")] public List<DiscoveredWebsiteDto> Websites { get; init; } = [];
    [JsonPropertyName("reviews")] public List<ReviewDto> Reviews { get; init; } = [];
    [JsonPropertyName("profiles")] public List<ThirdPartyProfileDto> Profiles { get; init; } = [];
    [JsonPropertyName("ga4MeasurementId")] public string? Ga4MeasurementId { get; init; }
    [JsonPropertyName("whatsAppEnabled")] public bool WhatsAppEnabled { get; init; }
}

public sealed record DiscoveredWebsiteDto
{
    [JsonPropertyName("url")] public string Url { get; init; } = default!;
    [JsonPropertyName("source")] public string Source { get; init; } = default!;
    [JsonPropertyName("html")] public string? Html { get; init; }
}

public sealed record ReviewDto
{
    [JsonPropertyName("text")] public string Text { get; init; } = default!;
    [JsonPropertyName("rating")] public int Rating { get; init; }
    [JsonPropertyName("reviewer")] public string Reviewer { get; init; } = default!;
    [JsonPropertyName("source")] public string Source { get; init; } = default!;
    [JsonPropertyName("date")] public DateTime? Date { get; init; }
}

public sealed record ThirdPartyProfileDto
{
    [JsonPropertyName("platform")] public string Platform { get; init; } = default!;
    [JsonPropertyName("bio")] public string? Bio { get; init; }
    [JsonPropertyName("reviews")] public List<ReviewDto> Reviews { get; init; } = [];
    [JsonPropertyName("salesCount")] public int? SalesCount { get; init; }
    [JsonPropertyName("activeListingCount")] public int? ActiveListingCount { get; init; }
    [JsonPropertyName("yearsExperience")] public int? YearsExperience { get; init; }
    [JsonPropertyName("specialties")] public List<string> Specialties { get; init; } = [];
    [JsonPropertyName("serviceAreas")] public List<string> ServiceAreas { get; init; } = [];
    [JsonPropertyName("recentSales")] public List<ListingInfoDto> RecentSales { get; init; } = [];
    [JsonPropertyName("activeListings")] public List<ListingInfoDto> ActiveListings { get; init; } = [];
}

public sealed record ListingInfoDto
{
    [JsonPropertyName("address")] public string Address { get; init; } = default!;
    [JsonPropertyName("city")] public string City { get; init; } = default!;
    [JsonPropertyName("state")] public string State { get; init; } = default!;
    [JsonPropertyName("price")] public string Price { get; init; } = default!;
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("beds")] public int? Beds { get; init; }
    [JsonPropertyName("baths")] public int? Baths { get; init; }
    [JsonPropertyName("sqft")] public int? Sqft { get; init; }
    [JsonPropertyName("imageUrl")] public string? ImageUrl { get; init; }
    [JsonPropertyName("date")] public DateTime? Date { get; init; }
}

// ── Phase 0 ───────────────────────────────────────────────────────────────────

/// <summary>Input to the CheckActivationComplete activity function.</summary>
public sealed record CheckActivationCompleteInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
    [JsonPropertyName("languages")] public List<string>? Languages { get; init; }
}

/// <summary>Output from the CheckActivationComplete activity function.</summary>
public sealed record CheckActivationCompleteOutput
{
    [JsonPropertyName("isComplete")] public bool IsComplete { get; init; }
}

// ── Phase 2 Synthesis inputs ──────────────────────────────────────────────────

/// <summary>Shared corpus bundle passed to all Phase 2 synthesis workers.</summary>
public sealed record SynthesisInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
    [JsonPropertyName("agentName")] public string AgentName { get; init; } = default!;
    [JsonPropertyName("emailCorpus")] public EmailFetchOutput EmailCorpus { get; init; } = default!;
    [JsonPropertyName("driveIndex")] public DriveIndexOutput DriveIndex { get; init; } = default!;
    [JsonPropertyName("discovery")] public AgentDiscoveryOutput Discovery { get; init; } = default!;
}

// ── Phase 2 string outputs (workers returning string?) ────────────────────────

public sealed record StringOutput
{
    [JsonPropertyName("value")] public string? Value { get; init; }
}

/// <summary>Output from PipelineAnalysis activity — structured JSON + markdown summary.</summary>
public sealed record PipelineAnalysisOutput
{
    [JsonPropertyName("pipelineJson")] public string? PipelineJson { get; init; }
    [JsonPropertyName("markdown")] public string? Markdown { get; init; }
}

/// <summary>Output from VoiceExtraction activity.</summary>
public sealed record VoiceExtractionOutput
{
    [JsonPropertyName("voiceSkillMarkdown")] public string? VoiceSkillMarkdown { get; init; }
    [JsonPropertyName("isLowConfidence")] public bool IsLowConfidence { get; init; }
    [JsonPropertyName("localizedSkills")] public Dictionary<string, string>? LocalizedSkills { get; init; }
}

/// <summary>Output from Personality activity.</summary>
public sealed record PersonalityOutput
{
    [JsonPropertyName("personalitySkillMarkdown")] public string? PersonalitySkillMarkdown { get; init; }
    [JsonPropertyName("isLowConfidence")] public bool IsLowConfidence { get; init; }
    [JsonPropertyName("localizedSkills")] public Dictionary<string, string>? LocalizedSkills { get; init; }
}

/// <summary>Output from BrandingDiscovery activity.</summary>
public sealed record BrandingDiscoveryOutput
{
    [JsonPropertyName("brandingKitMarkdown")] public string? BrandingKitMarkdown { get; init; }
    [JsonPropertyName("kit")] public BrandingKitDto? Kit { get; init; }
}

public sealed record BrandingKitDto
{
    [JsonPropertyName("colors")] public List<ColorEntryDto> Colors { get; init; } = [];
    [JsonPropertyName("fonts")] public List<FontEntryDto> Fonts { get; init; } = [];
    [JsonPropertyName("logos")] public List<LogoVariantDto> Logos { get; init; } = [];
    [JsonPropertyName("recommendedTemplate")] public string? RecommendedTemplate { get; init; }
    [JsonPropertyName("templateReason")] public string? TemplateReason { get; init; }
}

public sealed record ColorEntryDto
{
    [JsonPropertyName("role")] public string Role { get; init; } = default!;
    [JsonPropertyName("hex")] public string Hex { get; init; } = default!;
    [JsonPropertyName("source")] public string Source { get; init; } = default!;
    [JsonPropertyName("usage")] public string Usage { get; init; } = default!;
}

public sealed record FontEntryDto
{
    [JsonPropertyName("role")] public string Role { get; init; } = default!;
    [JsonPropertyName("family")] public string Family { get; init; } = default!;
    [JsonPropertyName("weight")] public string Weight { get; init; } = default!;
    [JsonPropertyName("source")] public string Source { get; init; } = default!;
}

public sealed record LogoVariantDto
{
    [JsonPropertyName("variant")] public string Variant { get; init; } = default!;
    [JsonPropertyName("fileName")] public string FileName { get; init; } = default!;
    [JsonPropertyName("bytes")] public byte[] Bytes { get; init; } = default!;
    [JsonPropertyName("source")] public string Source { get; init; } = default!;
}

/// <summary>Output from CoachingAnalysis activity.</summary>
public sealed record CoachingOutput
{
    [JsonPropertyName("coachingReportMarkdown")] public string? CoachingReportMarkdown { get; init; }
    [JsonPropertyName("isInsufficient")] public bool IsInsufficient { get; init; }
}

/// <summary>Output from MarketingStyle activity (returns two strings plus localized skills).</summary>
public sealed record MarketingStyleOutput
{
    [JsonPropertyName("styleGuide")] public string? StyleGuide { get; init; }
    [JsonPropertyName("brandSignals")] public string? BrandSignals { get; init; }
    [JsonPropertyName("localizedSkills")] public Dictionary<string, string>? LocalizedSkills { get; init; }
}

/// <summary>Output from BrandExtraction activity.</summary>
public sealed record BrandExtractionOutput
{
    [JsonPropertyName("signals")] public string? Signals { get; init; }
    [JsonPropertyName("localizedSkills")] public Dictionary<string, string>? LocalizedSkills { get; init; }
}

/// <summary>Output from BrandVoice activity.</summary>
public sealed record BrandVoiceOutput
{
    [JsonPropertyName("signals")] public string? Signals { get; init; }
    [JsonPropertyName("localizedSkills")] public Dictionary<string, string>? LocalizedSkills { get; init; }
}

// ── Phase 2.5 Contact Detection ───────────────────────────────────────────────

public sealed record ContactDetectionInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
    [JsonPropertyName("driveExtractions")] public List<DocumentExtractionDto> DriveExtractions { get; init; } = [];
    [JsonPropertyName("emailCorpus")] public EmailFetchOutput EmailCorpus { get; init; } = default!;
}

public sealed record ContactDetectionOutput
{
    [JsonPropertyName("contacts")] public List<ImportedContactDto> Contacts { get; init; } = [];
}

public sealed record ImportedContactDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = default!;
    [JsonPropertyName("email")] public string? Email { get; init; }
    [JsonPropertyName("phone")] public string? Phone { get; init; }
    [JsonPropertyName("role")] public string Role { get; init; } = default!;
    [JsonPropertyName("stage")] public string Stage { get; init; } = default!;
    [JsonPropertyName("propertyAddress")] public string? PropertyAddress { get; init; }
    [JsonPropertyName("documents")] public List<DocumentReferenceDto> Documents { get; init; } = [];
}

public sealed record DocumentReferenceDto
{
    [JsonPropertyName("driveFileId")] public string DriveFileId { get; init; } = default!;
    [JsonPropertyName("fileName")] public string FileName { get; init; } = default!;
    [JsonPropertyName("type")] public string Type { get; init; } = default!;
    [JsonPropertyName("date")] public DateTime? Date { get; init; }
}

// ── Phase 3 Persist inputs ────────────────────────────────────────────────────

public sealed record PersistProfileInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
    [JsonPropertyName("handle")] public string Handle { get; init; } = default!;
    [JsonPropertyName("voice")] public VoiceExtractionOutput? Voice { get; init; }
    [JsonPropertyName("personality")] public PersonalityOutput? Personality { get; init; }
    [JsonPropertyName("cmaStyle")] public string? CmaStyle { get; init; }
    [JsonPropertyName("websiteStyle")] public string? WebsiteStyle { get; init; }
    [JsonPropertyName("salesPipeline")] public string? SalesPipeline { get; init; }
    [JsonPropertyName("coaching")] public CoachingOutput? Coaching { get; init; }
    [JsonPropertyName("branding")] public BrandingDiscoveryOutput? Branding { get; init; }
    [JsonPropertyName("compliance")] public string? Compliance { get; init; }
    [JsonPropertyName("pipelineJson")] public string? PipelineJson { get; init; }
    [JsonPropertyName("driveIndexMarkdown")] public string DriveIndexMarkdown { get; init; } = default!;
    [JsonPropertyName("discoveryMarkdown")] public string DiscoveryMarkdown { get; init; } = default!;
    [JsonPropertyName("emailSignatureMarkdown")] public string? EmailSignatureMarkdown { get; init; }
    [JsonPropertyName("headshotBytes")] public byte[]? HeadshotBytes { get; init; }
    [JsonPropertyName("brokerageLogoBytes")] public byte[]? BrokerageLogoBytes { get; init; }
    [JsonPropertyName("agentName")] public string? AgentName { get; init; }
    [JsonPropertyName("agentEmail")] public string AgentEmail { get; init; } = default!;
    [JsonPropertyName("agentPhone")] public string? AgentPhone { get; init; }
    [JsonPropertyName("agentTitle")] public string? AgentTitle { get; init; }
    [JsonPropertyName("agentLicenseNumber")] public string? AgentLicenseNumber { get; init; }
    [JsonPropertyName("serviceAreas")] public List<string> ServiceAreas { get; init; } = [];
    [JsonPropertyName("discovery")] public AgentDiscoveryOutput Discovery { get; init; } = default!;
    [JsonPropertyName("localizedSkills")] public Dictionary<string, string>? LocalizedSkills { get; init; }
}

public sealed record BrandMergeInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
    [JsonPropertyName("brandingKit")] public string BrandingKit { get; init; } = default!;
    [JsonPropertyName("voiceSkill")] public string VoiceSkill { get; init; } = default!;
}

public sealed record ContactImportInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
    [JsonPropertyName("contacts")] public List<ImportedContactDto> Contacts { get; init; } = [];
}

// ── Phase 4 Notify ────────────────────────────────────────────────────────────

public sealed record WelcomeNotificationInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
    [JsonPropertyName("handle")] public string Handle { get; init; } = default!;
    [JsonPropertyName("agentName")] public string? AgentName { get; init; }
    [JsonPropertyName("agentPhone")] public string? AgentPhone { get; init; }
    [JsonPropertyName("whatsAppEnabled")] public bool WhatsAppEnabled { get; init; }
    [JsonPropertyName("agentEmail")] public string? AgentEmail { get; init; }
    // Synthesis data for personalized welcome email
    [JsonPropertyName("voiceSkill")] public string? VoiceSkill { get; init; }
    [JsonPropertyName("personalitySkill")] public string? PersonalitySkill { get; init; }
    [JsonPropertyName("coachingReport")] public string? CoachingReport { get; init; }
    [JsonPropertyName("pipelineJson")] public string? PipelineJson { get; init; }
    [JsonPropertyName("contactCount")] public int ContactCount { get; init; }
    [JsonPropertyName("localizedSkills")] public Dictionary<string, string>? LocalizedSkills { get; init; }
}

public sealed record CleanupStagedContentInput
{
    [JsonPropertyName("accountId")] public string AccountId { get; init; } = default!;
    [JsonPropertyName("agentId")] public string AgentId { get; init; } = default!;
}
