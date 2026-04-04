# Language Observability Dashboard — Grafana Panels

**Date:** 2026-04-03
**Status:** Draft
**Author:** Eddie Rosado

---

All metrics flow through OTel (`RealEstateStar.Language` ActivitySource + Meter) to Grafana Cloud. This spec defines the dashboard panels for monitoring language features across both the Durable Functions activation and lead pipelines.

## Panel 1: Language Distribution (Activation)

**Purpose:** What percentage of agent emails are English vs Spanish during activation corpus tagging.

**Query:**
```promql
sum by (locale) (rate(language_emails_detected_total[1h]))
```

**Visualization:** Pie chart

**Labels:** `locale` (`en`, `es`)

## Panel 2: Per-Language Skill Extraction Rate

**Purpose:** How many skills are being extracted per language. Helps verify Spanish extraction is actually running for bilingual agents.

**Query:**
```promql
sum by (locale, skill_name) (rate(language_skills_extracted_total[1h]))
```

**Visualization:** Bar chart (grouped by locale, one bar per skill)

**Labels:** `locale`, `skill_name` (`VoiceSkill`, `PersonalitySkill`, `MarketingStyle`, `BrandExtraction`, `BrandVoice`)

## Panel 3: Low Confidence Skills

**Purpose:** Alert table showing when Spanish corpus data is insufficient for high-quality extraction. Maps to the `IsLowConfidence` flag on DF activity output DTOs (`VoiceExtractionOutput.IsLowConfidence`, `PersonalityOutput.IsLowConfidence`).

**Query:**
```promql
sum by (locale, skill_name) (rate(language_skills_low_confidence_total[1h]))
```

**Visualization:** Alert table (sorted by count descending)

**Labels:** `locale`, `skill_name`

## Panel 4: Lead Locale Distribution

**Purpose:** What languages leads are submitting in. Driven by `LeadOrchestratorInput.Locale` in the DF lead pipeline.

**Query:**
```promql
sum by (locale) (rate(language_lead_locale_total[1h]))
```

**Visualization:** Pie chart

**Labels:** `locale` (`en`, `es`)

## Panel 5: Email Drafting by Locale

**Purpose:** Track email drafts over time per language. Driven by `DraftLeadEmailInput.Locale` in the DF lead pipeline.

**Query:**
```promql
sum by (locale) (rate(language_email_drafted_total[1h]))
```

**Visualization:** Time series (one line per locale)

**Labels:** `locale`

## Panel 6: CMA Reports by Locale

**Purpose:** Counter showing Spanish vs English CMA PDF generation. Driven by `GeneratePdfInput.Locale` in the DF lead pipeline.

**Query:**
```promql
sum by (locale) (rate(language_cma_generated_total[1h]))
```

**Visualization:** Counter (stat panel, one per locale)

**Labels:** `locale`

## Panel 7: Language Detection Method

**Purpose:** Histogram showing latency of heuristic-based detection vs Claude fallback. The heuristic (`LanguageDetector.DetectLocale()`) should handle the vast majority of cases cheaply.

**Query:**
```promql
sum by (method) (rate(language_detection_duration_ms_bucket[1h]))
```

**Visualization:** Histogram (two series: `heuristic`, `claude_fallback`)

**Labels:** `method`

## Panel 8: Welcome Messages by Locale

**Purpose:** Track localized welcome messages sent per channel. Driven by `WelcomeNotificationInput.LocalizedSkills` in the DF activation pipeline.

**Query:**
```promql
sum by (locale, channel) (rate(language_welcome_sent_total[1h]))
```

**Visualization:** Stacked bar chart (x-axis: locale, stacked by channel)

**Labels:** `locale`, `channel` (`email`, `whatsapp`)

## Panel 9: Existing Metrics with Locale Dimension

These existing counters now carry a `locale` tag, enabling per-language filtering on existing dashboards:

| Existing Metric | Example Filter |
|----------------|---------------|
| `leads_received_total` | `{locale="es"}` vs `{locale="en"}` |
| `orchestrator_email_sent_total` | `{locale="es"}` |
| `cma_generated_total` | `{locale="es"}` |
| `activation_completed_total` | Tag: `languages_count` (1 = English-only, 2 = bilingual) |

**Sample queries:**

```promql
# Spanish leads as percentage of total
sum(rate(leads_received_total{locale="es"}[1h]))
/
sum(rate(leads_received_total[1h]))

# Activation completions by language count
sum by (languages_count) (rate(activation_completed_total[1h]))
```

## Alerting Rules

### Alert 1: Low Confidence Skills Detected

**Condition:** `language_skills_low_confidence_total` increments for any locale.

**Meaning:** Insufficient corpus data for quality skill extraction. The agent may need more Spanish emails/docs before activation produces useful Spanish skills.

```promql
sum by (locale, skill_name) (increase(language_skills_low_confidence_total[1h])) > 0
```

**Severity:** Warning
**Action:** Review the agent's corpus size. If < 10 Spanish items, consider waiting for more data before re-activating.

### Alert 2: High Language Detection Fallback Rate

**Condition:** Claude fallback detection rate exceeds 20% of total detections.

**Meaning:** The heuristic is failing too often, falling back to expensive Claude API calls. May indicate a new language pattern or data quality issue.

```promql
sum(rate(language_detection_duration_ms_count{method="claude_fallback"}[1h]))
/
sum(rate(language_detection_duration_ms_count[1h]))
> 0.2
```

**Severity:** Warning
**Action:** Review the heuristic stop-word list in `LanguageDetector`. Add missing patterns or adjust the confidence threshold.

## Dashboard JSON Template

Import into Grafana via **Dashboards > Import > Paste JSON**. The dashboard UID should be `language-observability` for cross-linking from the main system overview dashboard.

Panels should be arranged in a 2-column grid:
- Row 1: Panel 1 (pie) + Panel 4 (pie)
- Row 2: Panel 2 (bar) + Panel 3 (alert table)
- Row 3: Panel 5 (time series) + Panel 6 (counter)
- Row 4: Panel 7 (histogram) + Panel 8 (stacked bar)
- Row 5: Panel 9 (stat panels, full width)
