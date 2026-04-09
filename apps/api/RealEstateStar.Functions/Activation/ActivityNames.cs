namespace RealEstateStar.Functions.Activation;

/// <summary>
/// Canonical activity function names used by both the orchestrator and activity functions.
/// Keeping names in one place prevents typos between caller and callee.
/// </summary>
internal static class ActivityNames
{
    // Phase 0
    public const string CheckActivationComplete = "ActivationCheckComplete";

    // Phase 1: Gather
    public const string EmailFetch = "ActivationEmailFetch";
    public const string EmailTransactionExtraction = "ActivationEmailTransactionExtraction";
    public const string DriveIndex = "ActivationDriveIndex";
    public const string AgentDiscovery = "ActivationAgentDiscovery";

    // Phase 2: Synthesize
    public const string VoiceExtraction = "ActivationVoiceExtraction";
    public const string Personality = "ActivationPersonality";
    public const string BrandingDiscovery = "ActivationBrandingDiscovery";
    public const string CmaStyle = "ActivationCmaStyle";
    public const string MarketingStyle = "ActivationMarketingStyle";
    public const string WebsiteStyle = "ActivationWebsiteStyle";
    public const string PipelineAnalysis = "ActivationPipelineAnalysis";
    public const string Coaching = "ActivationCoaching";
    public const string BrandExtraction = "ActivationBrandExtraction";
    public const string BrandVoice = "ActivationBrandVoice";
    public const string ComplianceAnalysis = "ActivationComplianceAnalysis";
    public const string FeeStructure = "ActivationFeeStructure";

    // Phase 2.5: Contact Detection
    public const string ContactDetection = "ActivationContactDetection";

    // Phase 3: Persist + Merge
    public const string PersistProfile = "ActivationPersistProfile";
    public const string BrandMerge = "ActivationBrandMerge";
    public const string ContactImport = "ActivationContactImport";

    // Phase 3.5: Cleanup
    public const string CleanupStagedContent = "ActivationCleanupStagedContent";

    // Phase 4: Notify
    public const string WelcomeNotification = "ActivationWelcomeNotification";
}
