# Language as a First-Class Dimension — Design Spec

**Date:** 2026-04-02
**Status:** Draft
**Author:** Eddie Rosado

---

## Problem Statement

Agent sites now support multiple languages (PR #123), but the backend pipelines (activation, lead processing) are English-only. Bilingual agents like Jenise Buckalew — who communicates with clients in both English and Spanish — need per-language voice and personality extraction during activation. Spanish-speaking leads should receive Spanish emails and CMA reports that match the agent's authentic Spanish communication style.

Today, all activation workers produce a single English skill file (e.g., `Voice Skill.md`), and the lead pipeline drafts English-only emails regardless of the contact's language preference. This creates a jarring experience for Spanish-speaking leads interacting with a bilingual agent's Spanish-language website.

## Two-Axis Model

Language flows through the system on two independent axes:

| Axis | Source | Stored As | Used By |
|------|--------|-----------|---------|
| **Agent language capability** | Activation pipeline (Phase 2 workers) | Per-language skill files (`Voice Skill.es.md`) | Email drafter, CMA generator, future follow-up system |
| **Contact language preference** | Lead form submission (`Lead.Locale`) | `locale` field in Lead frontmatter | Email template selection, CMA PDF locale, notification language |

These axes combine at runtime: the email drafter loads the agent's skill for the contact's locale, producing a message that matches both the agent's authentic voice in that language and the contact's preference.

## Supported Languages

- **English** (`en`) — default, always available
- **Spanish** (`es`) — opt-in via `identity.languages` in agent config

The infrastructure is extensible to additional BCP 47 locale codes, but only English and Spanish are supported at launch.

## Locale Detection Flow

```mermaid
flowchart TD
    Browser["Browser<br/>Accept-Language header"] --> MW["agent-site middleware.ts<br/>resolveLocale()"]
    MW --> Cookie{"locale cookie<br/>set?"}
    Cookie -->|yes| UseCookie["Use cookie value"]
    Cookie -->|no| Parse["Parse Accept-Language<br/>match against agent languages"]
    Parse --> Supported{"Supported by<br/>agent config?"}
    Supported -->|yes| SetLocale["Set locale cookie + var"]
    Supported -->|no| DefaultEn["Default to 'en'"]

    UseCookie --> Page["Page renders in locale"]
    SetLocale --> Page
    DefaultEn --> Page

    Page --> Form["LeadForm component<br/>hidden field: locale"]
    Form --> API["POST /leads<br/>body includes locale"]
    API --> Lead["Lead.Locale persisted<br/>in YAML frontmatter"]
    Lead --> Drafter["Email drafter<br/>loads AgentContext.GetSkill('VoiceSkill', locale)"]
    Drafter --> Template["Email template<br/>localized strings"]
    Template --> Gmail["Gmail send"]

    style MW fill:#e3f2fd
    style Lead fill:#e8f5e9
    style Drafter fill:#f3e5f5
```

### Language Detection Algorithm

`LanguageDetector.DetectLocale(text)` in `Domain/Shared/Services/` uses a two-pass heuristic:

1. **Character-set check:** If the text contains characters from extended Latin sets common in Spanish (accented vowels, inverted punctuation), increment the Spanish signal score.
2. **Stop-word scoring:** Count occurrences of high-frequency Spanish stop words (`de`, `la`, `el`, `en`, `que`, `los`, `las`, `por`, `con`, `para`) vs. English stop words (`the`, `and`, `for`, `with`, `that`, `this`, `from`, `have`, `are`). The language with the higher normalized score wins.
3. **Threshold:** If neither language exceeds a 60% confidence threshold, default to `en`.

This heuristic is used during activation (Phase 1) to tag emails and documents by language before partitioning for Phase 2 extraction.

## Per-Language Skill Extraction (Durable Functions)

Language extraction is fully integrated with the Azure Durable Functions activation orchestrator. `LocalizedSkills` (a `Dictionary<string, string>` mapping `"{SkillName}.{locale}.md"` to markdown content) flows through DF serialization DTOs in `ActivationDtos.cs`.

```mermaid
flowchart TD
    subgraph Phase1["Phase 1: Gather + Tag (DF Activities)"]
        direction TB
        Emails["EmailFetchFunction<br/>Activity → 100 sent + 100 inbox"] --> Tag1["LanguageDetector.DetectLocale()<br/>per email"]
        Docs["DriveIndexFunction<br/>Activity → PDF + doc extraction"] --> Tag2["LanguageDetector.DetectLocale()<br/>per document"]
        Tag1 --> Tagged["Tagged corpus<br/>each item has .DetectedLocale"]
        Tag2 --> Tagged
    end

    subgraph Phase2["Phase 2: Partition + Extract (DF Activities)"]
        direction TB
        Tagged --> Partition["Partition by locale<br/>en corpus, es corpus"]
        Partition --> EnWorkers["English DF activities (12)<br/>VoiceExtractionFunction, PersonalityFunction, etc."]
        Partition --> EsWorkers["Spanish DF activities (12)<br/>same functions, es corpus<br/>only if es corpus >= 10 items"]
        EnWorkers --> EnSkills["VoiceExtractionOutput.VoiceSkillMarkdown<br/>PersonalityOutput.PersonalitySkillMarkdown<br/>etc."]
        EsWorkers --> EsSkills["VoiceExtractionOutput.LocalizedSkills['es']<br/>PersonalityOutput.LocalizedSkills['es']<br/>etc."]
    end

    subgraph Phase3["Phase 3: Persist (DF Activity)"]
        direction TB
        EnSkills --> Persist["PersistProfileFunction<br/>PersistProfileInput.LocalizedSkills<br/>fan-out write"]
        EsSkills --> Persist
        Persist --> Drive["Google Drive<br/>real-estate-star/{agentId}/"]
        Persist --> Blob["Azure Blob Storage"]
    end

    subgraph Phase0["Phase 0: Completion Check"]
        direction TB
        Check["CheckActivationCompleteFunction<br/>Languages=['es'] → also checks<br/>Voice Skill.es.md, Personality Skill.es.md"]
    end

    style Phase1 fill:#e3f2fd
    style Phase2 fill:#f3e5f5
    style Phase3 fill:#e8f5e9
    style Phase0 fill:#fff3e0
```

### DF Serialization

Each Phase 2 worker output DTO includes an optional `LocalizedSkills` dictionary:
- `VoiceExtractionOutput.LocalizedSkills` — `{"Voice Skill.es.md": "..."}`
- `PersonalityOutput.LocalizedSkills` — `{"Personality Skill.es.md": "..."}`
- `MarketingStyleOutput.LocalizedSkills` — `{"Marketing Style.es.md": "..."}`
- `BrandExtractionOutput.LocalizedSkills` — `{"Brand Extraction.es.md": "..."}`
- `BrandVoiceOutput.LocalizedSkills` — `{"Brand Voice.es.md": "..."}`

These are aggregated into `PersistProfileInput.LocalizedSkills` and written alongside English files. The DF JSON serializer handles the `IReadOnlyDictionary<string, string>` round-trip automatically.

### Minimum Corpus Threshold

Spanish skill extraction only runs if the tagged Spanish corpus contains at least 10 items (emails + documents combined). Below this threshold, there is insufficient data for Claude to extract a meaningful voice or personality profile. The agent still supports Spanish on the website, but email drafts fall back to English skills with a Spanish translation prompt.

### Per-Language File Naming

| English (default) | Spanish |
|-------------------|---------|
| `Voice Skill.md` | `Voice Skill.es.md` |
| `Personality Skill.md` | `Personality Skill.es.md` |
| `Marketing Style.md` | `Marketing Style.es.md` |
| `Brand Voice.md` | `Brand Voice.es.md` |

Convention: `{Skill Name}.{locale}.md` where locale is omitted for English (the default).

## Locale Flow in Lead Pipeline (Durable Functions)

When a lead submits through the agent site, `Lead.Locale` flows through the DF orchestrator chain via serialized DTOs:

1. **Form submission:** The `LeadForm` component includes a hidden `locale` field set to the page's resolved locale.
2. **API endpoint:** `Lead.Locale` is persisted in the lead's YAML frontmatter (`locale: es`).
3. **Queue trigger:** `StartLeadProcessingFunction` reads `Lead.Locale` and maps it into `LeadOrchestratorInput.Locale`.
4. **DF orchestrator:** `LeadOrchestratorFunction` propagates `Locale` into downstream activity DTOs:
   - `DraftLeadEmailInput.Locale` — email drafter loads `AgentContext.GetSkill("VoiceSkill", locale)`
   - `GeneratePdfInput.Locale` — CMA PDF renders localized headers, labels, disclaimer text
   - `NotifyAgentInput.Locale` — agent notification includes lead's language preference
   - `PersistLeadResultsInput.Locale` — final results record the locale used
5. **Email drafter activity:** If the locale-specific skill exists (`Voice Skill.es.md`), it uses the agent's authentic Spanish voice. If not, falls back to the English skill with a system prompt instructing Claude to draft in Spanish.
6. **Agent notification:** The notification to the agent (email/WhatsApp) is always in English — the agent manages their pipeline in English regardless of the contact's language.
7. **TCPA consent:** Consent text stays in English regardless of locale (legal requirement — TCPA consent must be in the language the regulation was written in).

## Observability

### ActivitySource

`RealEstateStar.Language` ActivitySource with spans:

| Span | Tags | Description |
|------|------|-------------|
| `language.detect` | `input_length`, `detected_locale`, `confidence` | Per-text language detection |
| `language.skill_load` | `skill_name`, `requested_locale`, `resolved_locale`, `fallback` | Skill file resolution with fallback tracking |
| `language.corpus_partition` | `en_count`, `es_count`, `total_count` | Phase 1 corpus partitioning |

### Meter

`RealEstateStar.Language` Meter:

| Metric | Type | Dimensions |
|--------|------|------------|
| `language.detection.count` | Counter | `detected_locale` |
| `language.skill_fallback.count` | Counter | `skill_name`, `requested_locale` |
| `language.corpus.size` | Histogram | `locale` |

### Existing Metric Enrichment

Add `locale` tag to existing metrics:
- `lead.submitted` — tag with `Lead.Locale`
- `email.drafted` — tag with locale used
- `cma.generated` — tag with locale used

## Follow-Up Wiring

`Lead.Locale` is persisted in the lead's YAML frontmatter from day one. When the follow-up system is built, it reads `Lead.Locale` to:
- Select the correct per-language voice skill for follow-up drafts
- Render follow-up email templates in the contact's language
- Schedule follow-ups at culturally appropriate intervals

No additional migration is needed — the locale field is already in place.

## Security Considerations

- Language detection input is the agent's own emails/documents (trusted data), not user input
- Lead locale comes from a hidden form field validated against the agent's supported languages array — arbitrary locale values are rejected
- Per-language skill files follow the same storage and access patterns as English skills (no new attack surface)
- TCPA consent text is never translated (legal requirement)

## Non-Goals

- Machine translation of skill files (skills are extracted from the agent's actual communications, not translated)
- More than two languages at launch
- Locale-specific pricing or billing
- Right-to-left language support
