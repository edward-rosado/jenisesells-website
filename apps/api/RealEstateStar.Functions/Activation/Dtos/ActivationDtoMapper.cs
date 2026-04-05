using RealEstateStar.Domain.Activation.Models;

namespace RealEstateStar.Functions.Activation.Dtos;

/// <summary>
/// Converts between domain models and Durable Function activity boundary DTOs.
/// All members are static — no state required.
/// </summary>
internal static class ActivationDtoMapper
{
    // ── Domain → DTO ──────────────────────────────────────────────────────────

    public static EmailFetchOutput ToDto(EmailCorpus corpus) =>
        new()
        {
            SentEmails = corpus.SentEmails.Select(ToDto).ToList(),
            InboxEmails = corpus.InboxEmails.Select(ToDto).ToList(),
            Signature = corpus.Signature is null ? null : ToDto(corpus.Signature)
        };

    public static EmailMessageDto ToDto(EmailMessage m) =>
        new()
        {
            Id = m.Id,
            Subject = m.Subject,
            Body = m.Body,
            From = m.From,
            To = m.To,
            Date = m.Date,
            SignatureBlock = m.SignatureBlock
        };

    public static EmailSignatureDto ToDto(EmailSignature s) =>
        new()
        {
            Name = s.Name,
            Title = s.Title,
            Phone = s.Phone,
            LicenseNumber = s.LicenseNumber,
            BrokerageName = s.BrokerageName,
            SocialLinks = s.SocialLinks.ToList(),
            HeadshotUrl = s.HeadshotUrl,
            WebsiteUrl = s.WebsiteUrl,
            LogoUrl = s.LogoUrl
        };

    public static DriveIndexOutput ToDto(DriveIndex idx) =>
        new()
        {
            FolderId = idx.FolderId,
            Files = idx.Files.Select(ToDto).ToList(),
            // Cap each document to 8KB to prevent OOM on Azure Consumption plan.
            // Full contents are available to workers via GDriveClient if needed.
            Contents = idx.Contents.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Length > 8192 ? kvp.Value[..8192] : kvp.Value),
            DiscoveredUrls = idx.DiscoveredUrls.ToList(),
            Extractions = idx.Extractions.Select(ToDto).ToList()
        };

    public static DriveFileDto ToDto(DriveFile f) =>
        new()
        {
            Id = f.Id,
            Name = f.Name,
            MimeType = f.MimeType,
            Category = f.Category,
            ModifiedDate = f.ModifiedDate
        };

    public static DocumentExtractionDto ToDto(DocumentExtraction d) =>
        new()
        {
            DriveFileId = d.DriveFileId,
            FileName = d.FileName,
            Type = d.Type.ToString(),
            Clients = d.Clients.Select(ToDto).ToList(),
            Property = d.Property is null ? null : ToDto(d.Property),
            Date = d.Date,
            KeyTerms = d.KeyTerms is null ? null : ToDto(d.KeyTerms)
        };

    public static ExtractedClientDto ToDto(ExtractedClient c) =>
        new()
        {
            Name = c.Name,
            Role = c.Role.ToString(),
            Email = c.Email,
            Phone = c.Phone
        };

    public static ExtractedPropertyDto ToDto(ExtractedProperty p) =>
        new()
        {
            Address = p.Address,
            City = p.City,
            State = p.State,
            Zip = p.Zip
        };

    public static ExtractedKeyTermsDto ToDto(ExtractedKeyTerms k) =>
        new()
        {
            Price = k.Price,
            Commission = k.Commission,
            Contingencies = k.Contingencies.ToList()
        };

    public static AgentDiscoveryOutput ToDto(AgentDiscovery d) =>
        new()
        {
            HeadshotBytes = d.HeadshotBytes,
            LogoBytes = d.LogoBytes,
            Phone = d.Phone,
            Websites = d.Websites.Select(ToDto).ToList(),
            Reviews = d.Reviews.Select(ToDto).ToList(),
            Profiles = d.Profiles.Select(ToDto).ToList(),
            Ga4MeasurementId = d.Ga4MeasurementId,
            WhatsAppEnabled = d.WhatsAppEnabled
        };

    public static DiscoveredWebsiteDto ToDto(DiscoveredWebsite w) =>
        new()
        {
            Url = w.Url,
            Source = w.Source,
            Html = w.Html
        };

    public static ReviewDto ToDto(Review r) =>
        new()
        {
            Text = r.Text,
            Rating = r.Rating,
            Reviewer = r.Reviewer,
            Source = r.Source,
            Date = r.Date
        };

    public static ThirdPartyProfileDto ToDto(ThirdPartyProfile p) =>
        new()
        {
            Platform = p.Platform,
            Bio = p.Bio,
            Reviews = p.Reviews.Select(ToDto).ToList(),
            SalesCount = p.SalesCount,
            ActiveListingCount = p.ActiveListingCount,
            YearsExperience = p.YearsExperience,
            Specialties = p.Specialties.ToList(),
            ServiceAreas = p.ServiceAreas.ToList(),
            RecentSales = p.RecentSales.Select(ToDto).ToList(),
            ActiveListings = p.ActiveListings.Select(ToDto).ToList()
        };

    public static ListingInfoDto ToDto(ListingInfo l) =>
        new()
        {
            Address = l.Address,
            City = l.City,
            State = l.State,
            Price = l.Price,
            Status = l.Status,
            Beds = l.Beds,
            Baths = l.Baths,
            Sqft = l.Sqft,
            ImageUrl = l.ImageUrl,
            Date = l.Date
        };

    public static ImportedContactDto ToDto(ImportedContact c) =>
        new()
        {
            Name = c.Name,
            Email = c.Email,
            Phone = c.Phone,
            Role = c.Role.ToString(),
            Stage = c.Stage.ToString(),
            PropertyAddress = c.PropertyAddress,
            Documents = c.Documents.Select(ToDto).ToList()
        };

    public static DocumentReferenceDto ToDto(DocumentReference r) =>
        new()
        {
            DriveFileId = r.DriveFileId,
            FileName = r.FileName,
            Type = r.Type.ToString(),
            Date = r.Date
        };

    // ── DTO → Domain ──────────────────────────────────────────────────────────

    public static EmailCorpus ToDomain(EmailFetchOutput dto) =>
        new(
            SentEmails: dto.SentEmails.Select(ToDomain).ToList(),
            InboxEmails: dto.InboxEmails.Select(ToDomain).ToList(),
            Signature: dto.Signature is null ? null : ToDomain(dto.Signature));

    public static EmailMessage ToDomain(EmailMessageDto dto) =>
        new(dto.Id, dto.Subject, dto.Body, dto.From, dto.To, dto.Date, dto.SignatureBlock);

    public static EmailSignature ToDomain(EmailSignatureDto dto) =>
        new(dto.Name, dto.Title, dto.Phone, dto.LicenseNumber, dto.BrokerageName,
            dto.SocialLinks, dto.HeadshotUrl, dto.WebsiteUrl, dto.LogoUrl);

    public static DriveIndex ToDomain(DriveIndexOutput dto) =>
        new(
            FolderId: dto.FolderId,
            Files: dto.Files.Select(ToDomain).ToList(),
            Contents: dto.Contents,
            DiscoveredUrls: dto.DiscoveredUrls,
            Extractions: dto.Extractions.Select(ToDomain).ToList());

    public static DriveFile ToDomain(DriveFileDto dto) =>
        new(dto.Id, dto.Name, dto.MimeType, dto.Category, dto.ModifiedDate);

    public static DocumentExtraction ToDomain(DocumentExtractionDto dto) =>
        new(
            DriveFileId: dto.DriveFileId,
            FileName: dto.FileName,
            Type: Enum.Parse<DocumentType>(dto.Type),
            Clients: dto.Clients.Select(ToDomain).ToList(),
            Property: dto.Property is null ? null : ToDomain(dto.Property),
            Date: dto.Date,
            KeyTerms: dto.KeyTerms is null ? null : ToDomain(dto.KeyTerms));

    public static ExtractedClient ToDomain(ExtractedClientDto dto) =>
        new(dto.Name, Enum.Parse<ContactRole>(dto.Role), dto.Email, dto.Phone);

    public static ExtractedProperty ToDomain(ExtractedPropertyDto dto) =>
        new(dto.Address, dto.City, dto.State, dto.Zip);

    public static ExtractedKeyTerms ToDomain(ExtractedKeyTermsDto dto) =>
        new(dto.Price, dto.Commission, dto.Contingencies);

    public static AgentDiscovery ToDomain(AgentDiscoveryOutput dto) =>
        new(
            HeadshotBytes: dto.HeadshotBytes,
            LogoBytes: dto.LogoBytes,
            Phone: dto.Phone,
            Websites: dto.Websites.Select(ToDomain).ToList(),
            Reviews: dto.Reviews.Select(ToDomain).ToList(),
            Profiles: dto.Profiles.Select(ToDomain).ToList(),
            Ga4MeasurementId: dto.Ga4MeasurementId,
            WhatsAppEnabled: dto.WhatsAppEnabled);

    public static DiscoveredWebsite ToDomain(DiscoveredWebsiteDto dto) =>
        new(dto.Url, dto.Source, dto.Html);

    public static Review ToDomain(ReviewDto dto) =>
        new(dto.Text, dto.Rating, dto.Reviewer, dto.Source, dto.Date);

    public static ThirdPartyProfile ToDomain(ThirdPartyProfileDto dto) =>
        new(
            Platform: dto.Platform,
            Bio: dto.Bio,
            Reviews: dto.Reviews.Select(ToDomain).ToList(),
            SalesCount: dto.SalesCount,
            ActiveListingCount: dto.ActiveListingCount,
            YearsExperience: dto.YearsExperience,
            Specialties: dto.Specialties,
            ServiceAreas: dto.ServiceAreas,
            RecentSales: dto.RecentSales.Select(ToDomain).ToList(),
            ActiveListings: dto.ActiveListings.Select(ToDomain).ToList());

    public static ListingInfo ToDomain(ListingInfoDto dto) =>
        new(dto.Address, dto.City, dto.State, dto.Price, dto.Status, dto.Beds, dto.Baths, dto.Sqft, dto.ImageUrl, dto.Date);

    public static ImportedContact ToDomain(ImportedContactDto dto) =>
        new(
            Name: dto.Name,
            Email: dto.Email,
            Phone: dto.Phone,
            Role: Enum.Parse<ContactRole>(dto.Role),
            Stage: Enum.Parse<PipelineStage>(dto.Stage),
            PropertyAddress: dto.PropertyAddress,
            Documents: dto.Documents.Select(ToDomain).ToList());

    public static DocumentReference ToDomain(DocumentReferenceDto dto) =>
        new(dto.DriveFileId, dto.FileName, Enum.Parse<DocumentType>(dto.Type), dto.Date);

    public static BrandingKit? ToDomain(BrandingKitDto? dto)
    {
        if (dto is null) return null;
        return new BrandingKit(
            Colors: dto.Colors.Select(c => new ColorEntry(c.Role, c.Hex, c.Source, c.Usage)).ToList(),
            Fonts: dto.Fonts.Select(f => new FontEntry(f.Role, f.Family, f.Weight, f.Source)).ToList(),
            Logos: dto.Logos.Select(l => new LogoVariant(l.Variant, l.FileName, l.Bytes, l.Source)).ToList(),
            RecommendedTemplate: dto.RecommendedTemplate,
            TemplateReason: dto.TemplateReason);
    }

    /// <summary>Assembles the full ActivationOutputs from all Phase 2 activity results for Phase 3.</summary>
    public static ActivationOutputs BuildActivationOutputs(
        PersistProfileInput input)
    {
        return new ActivationOutputs
        {
            VoiceSkill = input.Voice?.VoiceSkillMarkdown,
            PersonalitySkill = input.Personality?.PersonalitySkillMarkdown,
            CmaStyleGuide = input.CmaStyle,
            MarketingStyle = input.Marketing?.StyleGuide,
            WebsiteStyleGuide = input.WebsiteStyle,
            SalesPipeline = input.SalesPipeline,
            CoachingReport = input.Coaching?.CoachingReportMarkdown,
            BrandingKitMarkdown = input.Branding?.BrandingKitMarkdown,
            BrandingKit = ToDomain(input.Branding?.Kit),
            BrandExtractionSignals = input.BrandExtraction,
            BrandVoiceSignals = input.BrandVoice,
            ComplianceAnalysis = input.Compliance,
            FeeStructure = input.FeeStructure,
            DriveIndex = input.DriveIndexMarkdown,
            AgentDiscoveryMarkdown = input.DiscoveryMarkdown,
            EmailSignature = input.EmailSignatureMarkdown,
            HeadshotBytes = input.HeadshotBytes,
            BrokerageLogoBytes = input.BrokerageLogoBytes,
            Discovery = ToDomain(input.Discovery),
            AgentName = input.AgentName,
            AgentEmail = input.AgentEmail,
            AgentPhone = input.AgentPhone,
            AgentTitle = input.AgentTitle,
            AgentLicenseNumber = input.AgentLicenseNumber,
            ServiceAreas = input.ServiceAreas,
        };
    }
}
