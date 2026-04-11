# Activation Pipeline Cost Model

> Last updated: 2026-04-11
> Pricing: Anthropic API rates as of April 2026

## Pricing Reference

| Model | Input | Output | Use Case |
|-------|-------|--------|----------|
| Opus 4.6 | $15/M tokens | $45/M tokens | Voice extraction (highest quality) |
| Sonnet 4.6 | $3/M tokens | $15/M tokens | Most synthesis + CMA + home search |
| Haiku 4.5 | $0.80/M tokens | $4/M tokens | Email classification, email drafting, contact detection |

---

## One-Time Costs (Agent Onboarding)

Activation runs once when an agent connects their Gmail + Google Drive.

### Phase 1: Data Gathering (No Claude)

| Activity | Cost | Notes |
|----------|------|-------|
| EmailFetch | $0 | Gmail API only |
| DriveIndex (PDF Vision) | ~$0.83 | Opus Vision on up to 10 PDFs |
| EmailTransactionExtraction | ~$0.17 | Sonnet, 4 batches of 5 emails |
| AgentDiscovery | $0 | Web scraping, no Claude |

### Phase 1.5: Email Classification (NEW)

| Activity | Model | Input | Output | Cost |
|----------|-------|-------|--------|------|
| EmailClassification | Haiku | ~6K tokens | ~2K tokens | **~$0.01** |

### Phase 2: Synthesis Workers (12 workers, paired)

| Worker | Model | Input Est. | Output Max | Cost Est. |
|--------|-------|-----------|------------|-----------|
| VoiceExtraction | **Opus** | ~10K | 8K | **$0.51** |
| Personality | Sonnet | ~10K | 6K | $0.12 |
| BrandingDiscovery | — | — | — | $0 (regex) |
| WebsiteStyle | Sonnet | ~10K | 2K | $0.06 |
| CmaStyle | Sonnet | ~6K | 2K | $0.05 |
| PipelineAnalysis | Sonnet | ~14K | 2K | $0.07 |
| Coaching | Sonnet | ~16K | 8K | $0.17 |
| ComplianceAnalysis | Sonnet | ~7K | 2K | $0.05 |
| FeeStructure | Sonnet | ~8K | 2K | $0.05 |
| BrandExtraction | Sonnet | ~10K | 2K | $0.06 |
| BrandVoice | Sonnet | ~10K | 2K | $0.06 |
| MarketingStyle | Sonnet | ~8K | 2K | $0.05 |

### Phase 2.25: Synthesis Merge (NEW)

| Activity | Model | Input | Output | Cost |
|----------|-------|-------|--------|------|
| SynthesisMerge | Sonnet | ~16K tokens | 4K | **~$0.11** |

Conditional — only runs when both Voice and Personality succeed.

### Phase 3-4: Persist + Notify

| Activity | Model | Input | Output | Cost |
|----------|-------|-------|--------|------|
| Welcome email draft | Sonnet | ~4K | ~400 | $0.06 |
| Brand merge | Sonnet | ~4K | ~1K | $0.06 |
| Profile scraping | Haiku | ~3K | ~700 | $0.01 |

### Activation Total

| Scenario | Cost |
|----------|------|
| **English-only agent** | **~$2.51** |
| **Bilingual agent (en+es)** | **~$3.06** |

Bilingual adds ~$0.55 for parallel Spanish extraction in 5 workers (Voice, Personality, BrandExtraction, BrandVoice, MarketingStyle).

---

## Recurring Costs (Per Lead)

Every lead triggers 1-3 Claude calls depending on lead type.

| Service | Model | Tokens | Cost/Call | Trigger |
|---------|-------|--------|-----------|---------|
| Email drafting | Haiku | ~3K | **$0.014** | Every lead |
| CMA analysis | Sonnet | ~5K | **$0.09** | Seller leads |
| Home search curation | Sonnet | ~3.5K | **$0.06** | Buyer leads |

### Per-Lead Cost

| Lead Type | Claude Calls | Cost |
|-----------|-------------|------|
| Buyer lead | email + home search | **$0.07** |
| Seller lead | email + CMA | **$0.10** |
| Seller + home search | email + CMA + home search | **$0.16** |

---

## Monthly Projection (Per Agent)

Assumes 60/40 buyer/seller lead mix.

| Lead Volume | Leads/mo | Monthly Claude Cost |
|-------------|----------|-------------------|
| Low | 5 | **$0.41** |
| Medium | 15 | **$1.26** |
| High | 30 | **$2.55** |
| Very high | 50 | **$4.25** |

---

## Unit Economics

Revenue: $14.99/agent/month

| | Low (5) | Medium (15) | High (30) | Very High (50) |
|---|---------|-------------|-----------|----------------|
| Ongoing Claude | $0.41 | $1.26 | $2.55 | $4.25 |
| Activation (amort 12mo) | $0.21 | $0.21 | $0.21 | $0.21 |
| **Total AI cost** | **$0.62** | **$1.47** | **$2.76** | **$4.46** |
| **Margin** | **$14.37 (95.9%)** | **$13.52 (90.2%)** | **$12.23 (81.6%)** | **$10.53 (70.2%)** |

---

## Cost Drivers (Ranked)

1. **Lead volume** — linear scaling, each lead costs $0.07-$0.16
2. **Seller lead ratio** — CMA (Sonnet, $0.09) is the most expensive per-lead call
3. **VoiceExtraction on Opus** — $0.51, or 20% of activation cost; downgrading to Sonnet would save ~$0.40
4. **Bilingual agents** — +$0.55 activation, no ongoing impact (skills are pre-synthesized)
5. **DriveIndex PDF Vision** — $0.83, largest single activation cost; scales with PDF count

---

## Optimization Levers

| Lever | Savings | Trade-off |
|-------|---------|-----------|
| Voice → Sonnet (from Opus) | -$0.40/activation | Lower voice quality |
| CMA → Haiku (from Sonnet) | -$0.07/seller lead | Lower CMA narrative quality |
| Skip CMA for low-score leads | -$0.09/skipped lead | No CMA for cold leads |
| Cache CMA for same property | -$0.09/cache hit | Stale data if market shifts |
| Reduce PDF Vision limit (10→5) | -$0.40/activation | Fewer document insights |
| Skip bilingual if <3 items | Already implemented | — |
| Conditional SynthesisMerge | Already implemented | — |

---

## What the Optimizations Changed

The April 2026 pipeline optimizations added 2 new activities and modified 7 existing workers:

| Change | Cost Impact | Value |
|--------|------------|-------|
| EmailClassification (Haiku) | +$0.01/activation | Pre-classified email routing to workers |
| SynthesisMerge (Sonnet) | +$0.11/activation | Cross-referenced coaching, contradiction detection, strengths summary |
| Extraction passthrough | $0 | Structured data in prompts (no new calls) |
| Reviews in 4 new workers | ~+$0.004 | ~500 extra tokens/worker (negligible) |
| ReviewFormatter refactor | $0 | Code reuse, no cost change |
| **Net activation increase** | **+$0.13 (+5.2%)** | Better synthesis quality, no ongoing cost change |
| **Net ongoing increase** | **$0** | Lead pipeline unchanged |
