# Real Estate Star — Cost Benefit Analysis

## Revenue Model

- **Price:** $14.99/agent/month
- **Model:** 14-day free trial → monthly subscription
- **Target:** Small real estate agents and brokerages

## Cost Structure

### One-Time Costs (Per Agent Onboarding)

| Component | Cost | Amortized (12mo) |
|-----------|------|-------------------|
| Activation pipeline (12 synthesis workers) | $2.25 | $0.19/mo |
| Email classification (Haiku) | $0.02 | — |
| Synthesis merge (Sonnet) | $0.11 | — |
| Profile scraping | $0.01 | — |
| Welcome email draft | $0.06 | — |
| Brand merge | $0.06 | — |
| **Total onboarding** | **$2.51** | **$0.21/mo** |

### Recurring Costs (Per Lead)

| Service | Model | Cost/Call |
|---------|-------|----------|
| Email drafting | Haiku | $0.014 |
| CMA analysis | Sonnet | $0.09 |
| Home search curation | Sonnet | $0.06 |

**Per-lead cost by type:**
- Buyer lead: $0.07 (email + home search)
- Seller lead: $0.10 (email + CMA)
- Seller + home search: $0.16 (email + CMA + home search)

### Monthly Claude API Cost (Per Agent)

| Lead Volume | Monthly Cost |
|-------------|-------------|
| 5 leads/mo | $0.41 |
| 15 leads/mo | $1.26 |
| 30 leads/mo | $2.55 |
| 50 leads/mo | $4.25 |

### Infrastructure (Shared Across All Agents)

#### Before Cost Reduction

| Resource | Monthly Cost |
|----------|-------------|
| Container Apps (API) | $15-30 |
| Container Registry (ACR) | $5 |
| Functions (Flex Consumption) | $20-40 |
| Storage Accounts (2x) | $2-3 |
| Key Vault | $0.50-1 |
| Application Insights | $0-2 |
| **Total** | **$42-78** |

#### After Cost Reduction

| Resource | Monthly Cost | Change |
|----------|-------------|--------|
| Container Apps (API) | $15-30 | — |
| GitHub Container Registry | $0 | **-$5** |
| Functions (Y1 Consumption) | $0-5 | **-$20-35** |
| Storage Accounts (2x) | $2-3 | — |
| Key Vault | $0.50-1 | — |
| Application Insights | $0-2 | — |
| **Total** | **$18-41** | **-$24-37** |

### External APIs

| Service | Monthly Cost | Notes |
|---------|-------------|-------|
| Anthropic Claude | $100-300 | Scales with agent count + lead volume |
| ScraperAPI | $49+ | Agent discovery web scraping |
| RentCast | $50-100 | CMA comp data |
| Google APIs | $0-50 | Mostly free tier |
| **Total** | **$200-500** | |

## Unit Economics

### Per-Agent Margin (After Cost Reduction)

Assumes 100 agents, 15 leads/agent/month, infra cost of $30/mo:

| | Amount |
|---|--------|
| Revenue | $14.99 |
| Claude API (leads) | -$1.26 |
| Claude API (activation, amortized) | -$0.21 |
| Infrastructure (per-agent share) | -$0.30 |
| **Gross margin** | **$13.22 (88.2%)** |

### Breakeven Analysis

| Cost Category | Monthly Fixed | Variable (per agent) |
|---------------|---------------|---------------------|
| Infrastructure | $30 | $0 |
| External APIs (base) | $100 | $0 |
| Claude activation (amortized) | $0 | $0.21 |
| Claude leads (15/mo avg) | $0 | $1.26 |
| **Total** | **$130** | **$1.47/agent** |

**Breakeven:** $130 / ($14.99 - $1.47) = **10 agents**

### Scale Projections

| Agents | Revenue | AI Cost | Infra | External APIs | Profit | Margin |
|--------|---------|---------|-------|---------------|--------|--------|
| 10 | $150 | $15 | $30 | $130 | -$25 | -17% |
| 25 | $375 | $37 | $30 | $175 | $133 | 35% |
| 50 | $750 | $74 | $35 | $250 | $391 | 52% |
| 100 | $1,499 | $147 | $40 | $350 | $962 | 64% |
| 250 | $3,748 | $368 | $50 | $500 | $2,830 | 75% |

## Key Insights

1. **AI costs are the dominant variable cost** — Claude API scales linearly with agents and leads. Infrastructure is nearly flat.

2. **88% gross margin at scale** — once past breakeven (10 agents), each additional agent contributes ~$13 in margin.

3. **Activation is a rounding error** — $2.51 one-time per agent amortizes to $0.21/mo over 12 months. Even if an agent churns after 1 month, it's only 16.7% of that month's revenue.

4. **Infrastructure cost is fixed, not variable** — the $30/mo Azure bill doesn't change whether you have 1 agent or 100. This is the benefit of serverless (Y1 Consumption + Container Apps scale-to-zero).

5. **The biggest cost lever is lead volume, not agent count** — a single agent processing 50 leads/mo costs $4.25 in Claude API. The platform can absorb this at $14.99/mo revenue, but heavy-volume agents reduce margin from 88% to 70%.

6. **External APIs (ScraperAPI, RentCast) are the breakeven bottleneck** — the $100-200/mo base cost means you need ~10 agents just to cover fixed costs. These are candidates for elimination or replacement with in-house alternatives as the platform scales.
