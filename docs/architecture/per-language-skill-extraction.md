# Per-Language Skill Extraction

How the activation pipeline extracts per-language skill files from a bilingual agent's communications.

```mermaid
flowchart TD
    subgraph Phase1["Phase 1: Gather + Tag"]
        direction TB
        EmailFetch["EmailFetch Activity<br/>100 sent + 100 inbox"] --> Detect1["LanguageDetector.DetectLocale()<br/>per email body"]
        DriveIndex["DriveIndex Activity<br/>PDF extraction + doc content"] --> Detect2["LanguageDetector.DetectLocale()<br/>per document text"]

        Detect1 --> TaggedEmails["Tagged emails<br/>[{email, locale: 'en'}, {email, locale: 'es'}, ...]"]
        Detect2 --> TaggedDocs["Tagged documents<br/>[{doc, locale: 'en'}, {doc, locale: 'es'}, ...]"]
    end

    subgraph Phase2["Phase 2: Partition + Extract"]
        direction TB
        TaggedEmails --> Partition["Partition by detected locale"]
        TaggedDocs --> Partition

        Partition --> EnCorpus["English corpus"]
        Partition --> EsCorpus["Spanish corpus"]

        EsCorpus --> Threshold{"es items >= 10?"}
        Threshold -->|no| SkipEs["Skip Spanish extraction<br/>fallback to translation prompt"]

        EnCorpus --> EnWorkers["12 English workers<br/>(VoiceExtraction, Personality,<br/>Branding, CmaStyle, etc.)"]
        Threshold -->|yes| EsWorkers["12 Spanish workers<br/>(same worker types,<br/>Spanish corpus input)"]

        EnWorkers --> EnFiles["Voice Skill.md<br/>Personality Skill.md<br/>Marketing Style.md<br/>..."]
        EsWorkers --> EsFiles["Voice Skill.es.md<br/>Personality Skill.es.md<br/>Marketing Style.es.md<br/>..."]
    end

    subgraph Phase3["Phase 3: Persist"]
        direction TB
        EnFiles --> PersistActivity["PersistProfile Activity<br/>fan-out write to Drive + Blob"]
        EsFiles --> PersistActivity

        PersistActivity --> AgentDrive["Agent Google Drive<br/>real-estate-star/{agentId}/"]
        PersistActivity --> AzureBlob["Azure Blob Storage"]
    end

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

## Language Detection

`LanguageDetector.DetectLocale(text)` in `Domain/Shared/Services/`:

1. **Character-set heuristic:** Scores presence of accented characters and inverted punctuation
2. **Stop-word scoring:** Counts Spanish vs. English high-frequency words
3. **Threshold:** Requires 60% confidence; defaults to `en` below threshold

## Minimum Corpus Rule

Spanish workers only run when the tagged Spanish corpus has >= 10 items. Below this threshold, the agent's Spanish email drafts use the English voice skill with a Claude system prompt requesting Spanish output. This prevents low-quality skill extraction from insufficient data.

## Cost Impact

For bilingual agents, Phase 2 cost roughly doubles (24 workers instead of 12). Phase 1 cost is unchanged (tagging is a lightweight heuristic, not a Claude call). Estimated additional cost per activation: $0.40-$1.00 depending on corpus size.
