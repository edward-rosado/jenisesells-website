using System.Text.Json.Serialization;

namespace RealEstateStar.Functions.Activation.Dtos;

// ── Phase 1 Gather ────────────────────────────────────────────────────────────

/// <summary>Input to the EmailFetch activity function.</summary>
public sealed record EmailFetchInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId);

/// <summary>Output from the EmailFetch activity function — the full email corpus.</summary>
public sealed record EmailFetchOutput(
    [property: JsonPropertyName("sentEmails")] IReadOnlyList<EmailMessageDto> SentEmails,
    [property: JsonPropertyName("inboxEmails")] IReadOnlyList<EmailMessageDto> InboxEmails,
    [property: JsonPropertyName("signature")] EmailSignatureDto? Signature);

public sealed record EmailMessageDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("to")] string[] To,
    [property: JsonPropertyName("date")] DateTime Date,
    [property: JsonPropertyName("signatureBlock")] string? SignatureBlock);

public sealed record EmailSignatureDto(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("licenseNumber")] string? LicenseNumber,
    [property: JsonPropertyName("brokerageName")] string? BrokerageName,
    [property: JsonPropertyName("socialLinks")] IReadOnlyList<string> SocialLinks,
    [property: JsonPropertyName("headshotUrl")] string? HeadshotUrl,
    [property: JsonPropertyName("websiteUrl")] string? WebsiteUrl,
    [property: JsonPropertyName("logoUrl")] string? LogoUrl);

/// <summary>Input to the DriveIndex activity function.</summary>
public sealed record DriveIndexInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId);

/// <summary>Output from the DriveIndex activity — the indexed drive contents.</summary>
public sealed record DriveIndexOutput(
    [property: JsonPropertyName("folderId")] string FolderId,
    [property: JsonPropertyName("files")] IReadOnlyList<DriveFileDto> Files,
    [property: JsonPropertyName("contents")] IReadOnlyDictionary<string, string> Contents,
    [property: JsonPropertyName("discoveredUrls")] IReadOnlyList<string> DiscoveredUrls,
    [property: JsonPropertyName("extractions")] IReadOnlyList<DocumentExtractionDto> Extractions);

public sealed record DriveFileDto(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("mimeType")] string MimeType,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("modifiedDate")] DateTime ModifiedDate);

public sealed record DocumentExtractionDto(
    [property: JsonPropertyName("driveFileId")] string DriveFileId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("clients")] IReadOnlyList<ExtractedClientDto> Clients,
    [property: JsonPropertyName("property")] ExtractedPropertyDto? Property,
    [property: JsonPropertyName("date")] DateTime? Date,
    [property: JsonPropertyName("keyTerms")] ExtractedKeyTermsDto? KeyTerms);

public sealed record ExtractedClientDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("phone")] string? Phone);

public sealed record ExtractedPropertyDto(
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("zip")] string? Zip);

public sealed record ExtractedKeyTermsDto(
    [property: JsonPropertyName("price")] string? Price,
    [property: JsonPropertyName("commission")] string? Commission,
    [property: JsonPropertyName("contingencies")] IReadOnlyList<string> Contingencies);

/// <summary>Input to the AgentDiscovery activity function.</summary>
public sealed record AgentDiscoveryInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("agentName")] string AgentName,
    [property: JsonPropertyName("emailSignature")] EmailSignatureDto? EmailSignature);

/// <summary>Output from the AgentDiscovery activity.</summary>
public sealed record AgentDiscoveryOutput(
    [property: JsonPropertyName("headshotBytes")] byte[]? HeadshotBytes,
    [property: JsonPropertyName("logoBytes")] byte[]? LogoBytes,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("websites")] IReadOnlyList<DiscoveredWebsiteDto> Websites,
    [property: JsonPropertyName("reviews")] IReadOnlyList<ReviewDto> Reviews,
    [property: JsonPropertyName("profiles")] IReadOnlyList<ThirdPartyProfileDto> Profiles,
    [property: JsonPropertyName("ga4MeasurementId")] string? Ga4MeasurementId,
    [property: JsonPropertyName("whatsAppEnabled")] bool WhatsAppEnabled);

public sealed record DiscoveredWebsiteDto(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("html")] string? Html);

public sealed record ReviewDto(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("rating")] int Rating,
    [property: JsonPropertyName("reviewer")] string Reviewer,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("date")] DateTime? Date);

public sealed record ThirdPartyProfileDto(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("bio")] string? Bio,
    [property: JsonPropertyName("reviews")] IReadOnlyList<ReviewDto> Reviews,
    [property: JsonPropertyName("salesCount")] int? SalesCount,
    [property: JsonPropertyName("activeListingCount")] int? ActiveListingCount,
    [property: JsonPropertyName("yearsExperience")] int? YearsExperience,
    [property: JsonPropertyName("specialties")] IReadOnlyList<string> Specialties,
    [property: JsonPropertyName("serviceAreas")] IReadOnlyList<string> ServiceAreas,
    [property: JsonPropertyName("recentSales")] IReadOnlyList<ListingInfoDto> RecentSales,
    [property: JsonPropertyName("activeListings")] IReadOnlyList<ListingInfoDto> ActiveListings);

public sealed record ListingInfoDto(
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("city")] string City,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("price")] string Price,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("beds")] int? Beds,
    [property: JsonPropertyName("baths")] int? Baths,
    [property: JsonPropertyName("sqft")] int? Sqft,
    [property: JsonPropertyName("imageUrl")] string? ImageUrl,
    [property: JsonPropertyName("date")] DateTime? Date);

// ── Phase 0 ───────────────────────────────────────────────────────────────────

/// <summary>Input to the CheckActivationComplete activity function.</summary>
public sealed record CheckActivationCompleteInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId);

/// <summary>Output from the CheckActivationComplete activity function.</summary>
public sealed record CheckActivationCompleteOutput(
    [property: JsonPropertyName("isComplete")] bool IsComplete);

// ── Phase 2 Synthesis inputs ──────────────────────────────────────────────────

/// <summary>Shared corpus bundle passed to all Phase 2 synthesis workers.</summary>
public sealed record SynthesisInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("agentName")] string AgentName,
    [property: JsonPropertyName("emailCorpus")] EmailFetchOutput EmailCorpus,
    [property: JsonPropertyName("driveIndex")] DriveIndexOutput DriveIndex,
    [property: JsonPropertyName("discovery")] AgentDiscoveryOutput Discovery);

// ── Phase 2 string outputs (workers returning string?) ────────────────────────

public sealed record StringOutput(
    [property: JsonPropertyName("value")] string? Value);

/// <summary>Output from VoiceExtraction activity.</summary>
public sealed record VoiceExtractionOutput(
    [property: JsonPropertyName("voiceSkillMarkdown")] string? VoiceSkillMarkdown,
    [property: JsonPropertyName("isLowConfidence")] bool IsLowConfidence);

/// <summary>Output from Personality activity.</summary>
public sealed record PersonalityOutput(
    [property: JsonPropertyName("personalitySkillMarkdown")] string? PersonalitySkillMarkdown,
    [property: JsonPropertyName("isLowConfidence")] bool IsLowConfidence);

/// <summary>Output from BrandingDiscovery activity.</summary>
public sealed record BrandingDiscoveryOutput(
    [property: JsonPropertyName("brandingKitMarkdown")] string? BrandingKitMarkdown,
    [property: JsonPropertyName("kit")] BrandingKitDto? Kit);

public sealed record BrandingKitDto(
    [property: JsonPropertyName("colors")] IReadOnlyList<ColorEntryDto> Colors,
    [property: JsonPropertyName("fonts")] IReadOnlyList<FontEntryDto> Fonts,
    [property: JsonPropertyName("logos")] IReadOnlyList<LogoVariantDto> Logos,
    [property: JsonPropertyName("recommendedTemplate")] string? RecommendedTemplate,
    [property: JsonPropertyName("templateReason")] string? TemplateReason);

public sealed record ColorEntryDto(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("hex")] string Hex,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("usage")] string Usage);

public sealed record FontEntryDto(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("family")] string Family,
    [property: JsonPropertyName("weight")] string Weight,
    [property: JsonPropertyName("source")] string Source);

public sealed record LogoVariantDto(
    [property: JsonPropertyName("variant")] string Variant,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("bytes")] byte[] Bytes,
    [property: JsonPropertyName("source")] string Source);

/// <summary>Output from CoachingAnalysis activity.</summary>
public sealed record CoachingOutput(
    [property: JsonPropertyName("coachingReportMarkdown")] string? CoachingReportMarkdown,
    [property: JsonPropertyName("isInsufficient")] bool IsInsufficient);

/// <summary>Output from MarketingStyle activity (returns two strings).</summary>
public sealed record MarketingStyleOutput(
    [property: JsonPropertyName("styleGuide")] string? StyleGuide,
    [property: JsonPropertyName("brandSignals")] string? BrandSignals);

// ── Phase 2.5 Contact Detection ───────────────────────────────────────────────

public sealed record ContactDetectionInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("driveExtractions")] IReadOnlyList<DocumentExtractionDto> DriveExtractions,
    [property: JsonPropertyName("emailCorpus")] EmailFetchOutput EmailCorpus);

public sealed record ContactDetectionOutput(
    [property: JsonPropertyName("contacts")] IReadOnlyList<ImportedContactDto> Contacts);

public sealed record ImportedContactDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("stage")] string Stage,
    [property: JsonPropertyName("propertyAddress")] string? PropertyAddress,
    [property: JsonPropertyName("documents")] IReadOnlyList<DocumentReferenceDto> Documents);

public sealed record DocumentReferenceDto(
    [property: JsonPropertyName("driveFileId")] string DriveFileId,
    [property: JsonPropertyName("fileName")] string FileName,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("date")] DateTime? Date);

// ── Phase 3 Persist inputs ────────────────────────────────────────────────────

public sealed record PersistProfileInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("voice")] VoiceExtractionOutput? Voice,
    [property: JsonPropertyName("personality")] PersonalityOutput? Personality,
    [property: JsonPropertyName("cmaStyle")] string? CmaStyle,
    [property: JsonPropertyName("marketingStyle")] MarketingStyleOutput? Marketing,
    [property: JsonPropertyName("websiteStyle")] string? WebsiteStyle,
    [property: JsonPropertyName("salesPipeline")] string? SalesPipeline,
    [property: JsonPropertyName("coaching")] CoachingOutput? Coaching,
    [property: JsonPropertyName("branding")] BrandingDiscoveryOutput? Branding,
    [property: JsonPropertyName("brandExtraction")] string? BrandExtraction,
    [property: JsonPropertyName("brandVoice")] string? BrandVoice,
    [property: JsonPropertyName("compliance")] string? Compliance,
    [property: JsonPropertyName("feeStructure")] string? FeeStructure,
    [property: JsonPropertyName("driveIndexMarkdown")] string DriveIndexMarkdown,
    [property: JsonPropertyName("discoveryMarkdown")] string DiscoveryMarkdown,
    [property: JsonPropertyName("emailSignatureMarkdown")] string? EmailSignatureMarkdown,
    [property: JsonPropertyName("headshotBytes")] byte[]? HeadshotBytes,
    [property: JsonPropertyName("brokerageLogoBytes")] byte[]? BrokerageLogoBytes,
    [property: JsonPropertyName("agentName")] string? AgentName,
    [property: JsonPropertyName("agentEmail")] string AgentEmail,
    [property: JsonPropertyName("agentPhone")] string? AgentPhone,
    [property: JsonPropertyName("agentTitle")] string? AgentTitle,
    [property: JsonPropertyName("agentLicenseNumber")] string? AgentLicenseNumber,
    [property: JsonPropertyName("serviceAreas")] IReadOnlyList<string> ServiceAreas,
    [property: JsonPropertyName("discovery")] AgentDiscoveryOutput Discovery);

public sealed record BrandMergeInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("brandingKit")] string BrandingKit,
    [property: JsonPropertyName("voiceSkill")] string VoiceSkill);

public sealed record ContactImportInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("contacts")] IReadOnlyList<ImportedContactDto> Contacts);

// ── Phase 4 Notify ────────────────────────────────────────────────────────────

public sealed record WelcomeNotificationInput(
    [property: JsonPropertyName("accountId")] string AccountId,
    [property: JsonPropertyName("agentId")] string AgentId,
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("agentName")] string? AgentName,
    [property: JsonPropertyName("agentPhone")] string? AgentPhone,
    [property: JsonPropertyName("whatsAppEnabled")] bool WhatsAppEnabled);
