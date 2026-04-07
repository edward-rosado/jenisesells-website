# Language Locale Flow

End-to-end flow of locale resolution from browser through the Durable Functions lead pipeline to email delivery. `Lead.Locale` flows through `LeadOrchestratorInput.Locale` into all downstream DF activity DTOs.

```mermaid
flowchart TD
    Browser["Browser<br/>Accept-Language: es,en;q=0.9"] --> MW["agent-site middleware.ts<br/>resolveLocale()"]

    MW --> CookieCheck{"locale cookie<br/>exists?"}
    CookieCheck -->|yes| ReadCookie["Read cookie value"]
    CookieCheck -->|no| ParseHeader["Parse Accept-Language<br/>against agent config languages"]

    ParseHeader --> Validate{"Locale in agent's<br/>supported languages?<br/>(en, es)"}
    Validate -->|yes| SetCookie["Set locale cookie<br/>+ resolve"]
    Validate -->|no| FallbackEn["Fallback to 'en'"]

    ReadCookie --> PageRender
    SetCookie --> PageRender
    FallbackEn --> PageRender

    PageRender["Page renders<br/>content-{locale}.json strings"] --> LeadForm["LeadForm component<br/>hidden input: locale={resolved}"]

    LeadForm --> API["POST /accounts/{accountId}/agents/{agentId}/leads<br/>body: { ..., locale: 'es' }"]
    API --> ValidateLocale["Validate locale against<br/>agent config languages"]
    ValidateLocale --> Persist["Lead.Locale persisted<br/>in YAML frontmatter"]

    Persist --> Queue["Azure Queue 'lead-requests'"]
    Queue --> Trigger["StartLeadProcessingFunction<br/>[QueueTrigger]<br/>Maps Lead.Locale →<br/>LeadOrchestratorInput.Locale"]
    Trigger --> Orch["LeadOrchestratorFunction<br/>(Durable Orchestrator)<br/>Propagates Locale to activities"]

    Orch --> Drafter["DraftLeadEmailFunction<br/>[ActivityTrigger]<br/>DraftLeadEmailInput.Locale"]
    Drafter --> LoadSkill["AgentContext.GetSkill<br/>('VoiceSkill', locale)"]
    LoadSkill --> SkillCheck{"Voice Skill.es.md<br/>exists?"}
    SkillCheck -->|yes| UseLocaleSkill["Load per-language skill"]
    SkillCheck -->|no| FallbackSkill["Load Voice Skill.md (en)<br/>+ Spanish system prompt"]

    UseLocaleSkill --> Draft["Claude drafts email<br/>in contact's language"]
    FallbackSkill --> Draft

    Draft --> Template["Email template<br/>localized subject + body"]
    Template --> Gmail["Gmail send<br/>to contact"]

    Orch --> CMA["CmaProcessingFunction<br/>[ActivityTrigger]"]
    CMA --> PDF["GeneratePdfFunction<br/>GeneratePdfInput.Locale"]
    PDF --> CmaPdf["CmaPdfGenerator<br/>locale-aware headers + labels"]
    CmaPdf --> Blob["Azure Blob Storage"]

    Orch --> Notify["NotifyAgentFunction<br/>NotifyAgentInput.Locale"]
    Orch --> PersistResults["PersistLeadResultsFunction<br/>PersistLeadResultsInput.Locale"]

    style MW fill:#e3f2fd
    style Persist fill:#e8f5e9
    style Orch fill:#fff3e0
    style Drafter fill:#f3e5f5
    style CMA fill:#f3e5f5
```

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| Cookie takes priority over Accept-Language | User's explicit choice (language picker) overrides browser default |
| Locale validated server-side against agent config | Prevents arbitrary locale injection; only `en` and `es` accepted |
| Fallback to English skill + translation prompt | Agents with insufficient Spanish corpus still serve Spanish leads |
| Agent notifications always in English | Agent manages their pipeline in one language |
| TCPA consent stays English | Legal requirement regardless of contact locale |
