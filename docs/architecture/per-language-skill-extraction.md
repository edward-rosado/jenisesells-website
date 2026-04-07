# Per-Language Skill Extraction

How the activation Durable Functions orchestrator extracts per-language skill files from a bilingual agent's communications. `LocalizedSkills` flows through DF serialization DTOs (`ActivationDtos.cs`).

```mermaid
flowchart TD
    subgraph Phase0["Phase 0: Completion Check (DF Activity)"]
        direction TB
        Check["CheckActivationCompleteFunction<br/>Input: CheckActivationCompleteInput.Languages<br/>When Languages contains 'es':<br/>also checks Voice Skill.es.md,<br/>Personality Skill.es.md"]
    end

    subgraph Phase1["Phase 1: Gather + Tag (DF Activities)"]
        direction TB
        EmailFetch["EmailFetchFunction<br/>[ActivityTrigger]<br/>100 sent + 100 inbox"] --> Detect1["LanguageDetector.DetectLocale()<br/>per email body"]
        DriveIndex["DriveIndexFunction<br/>[ActivityTrigger]<br/>PDF extraction + doc content"] --> Detect2["LanguageDetector.DetectLocale()<br/>per document text"]

        Detect1 --> TaggedEmails["Tagged emails<br/>[{email, locale: 'en'}, {email, locale: 'es'}, ...]"]
        Detect2 --> TaggedDocs["Tagged documents<br/>[{doc, locale: 'en'}, {doc, locale: 'es'}, ...]"]
    end

    subgraph Phase2["Phase 2: Partition + Extract (DF Activities)"]
        direction TB
        TaggedEmails --> Partition["Partition by detected locale"]
        TaggedDocs --> Partition

        Partition --> EnCorpus["English corpus"]
        Partition --> EsCorpus["Spanish corpus"]

        EsCorpus --> Threshold{"es items >= 10?"}
        Threshold -->|no| SkipEs["Skip Spanish extraction<br/>fallback to translation prompt"]

        EnCorpus --> EnActivities["Phase 2 DF activities<br/>(8 MVP + 4 Future tier)<br/>VoiceExtraction, Personality,<br/>BrandExtraction, BrandVoice, etc."]
        Threshold -->|yes| EsActivities["Future-tier Spanish activities<br/>(BrandExtraction, BrandVoice,<br/>MarketingStyle)<br/>Output: LocalizedSkills dict"]

        EnActivities --> EnOutputs["VoiceExtractionOutput.VoiceSkillMarkdown<br/>PersonalityOutput.PersonalitySkillMarkdown<br/>..."]
        EsActivities --> EsOutputs["BrandExtractionOutput.LocalizedSkills<br/>BrandVoiceOutput.LocalizedSkills<br/>MarketingStyleOutput.LocalizedSkills"]
    end

    subgraph Phase3["Phase 3: Persist (DF Activity)"]
        direction TB
        EnOutputs --> PersistActivity["PersistProfileFunction<br/>[ActivityTrigger]<br/>PersistProfileInput.LocalizedSkills<br/>fan-out write to Drive + Blob"]
        EsOutputs --> PersistActivity

        PersistActivity --> AgentDrive["Agent Google Drive<br/>real-estate-star/{agentId}/"]
        PersistActivity --> AzureBlob["Azure Blob Storage"]
    end

    style Phase0 fill:#fff3e0
    style Phase1 fill:#e3f2fd
    style Phase2 fill:#f3e5f5
    style Phase3 fill:#e8f5e9
```

## File Naming Convention

| Locale | File Pattern | Example |
|--------|-------------|---------|
| `en` (default) | `{Skill Name}.md` | `Voice Skill.md` |
| `es` | `{Skill Name}.es.md` | `Voice Skill.es.md` |

English is the default and omits the locale suffix. All other locales use BCP 47 codes.

## DF DTO Flow

| DTO | `LocalizedSkills` Field | Description |
|-----|------------------------|-------------|
| `MarketingStyleOutput` | `Dictionary<string, string>?` | `{"MarketingStyle.es": "..."}` |
| `BrandExtractionOutput` | `Dictionary<string, string>?` | `{"BrandExtraction.es": "..."}` |
| `BrandVoiceOutput` | `Dictionary<string, string>?` | `{"BrandVoice.es": "..."}` |
| `PersistProfileInput` | `Dictionary<string, string>?` | Aggregated from the 3 above |
| `WelcomeNotificationInput` | `Dictionary<string, string>?` | For localized welcome messages |

> **Note:** VoiceExtraction and Personality are MVP-tier workers. They do NOT produce `LocalizedSkills`. Only Future-tier workers (BrandExtraction, BrandVoice, MarketingStyle) generate per-language variants.

## Language Detection

`LanguageDetector.DetectLocale(text)` in `Domain/Shared/Services/`:

1. **Character-set heuristic:** Scores presence of accented characters and inverted punctuation
2. **Stop-word scoring:** Counts Spanish vs. English high-frequency words
3. **Threshold:** Requires 60% confidence; defaults to `en` below threshold

## Minimum Corpus Rule

Spanish DF activities only run when the tagged Spanish corpus has >= 10 items. Below this threshold, the agent's Spanish email drafts use the English voice skill with a Claude system prompt requesting Spanish output. This prevents low-quality skill extraction from insufficient data.

## Cost Impact

For bilingual agents, Phase 2 cost roughly doubles (24 DF activities instead of 12). Phase 1 cost is unchanged (tagging is a lightweight heuristic, not a Claude call). Estimated additional cost per activation: $0.40-$1.00 depending on corpus size.
