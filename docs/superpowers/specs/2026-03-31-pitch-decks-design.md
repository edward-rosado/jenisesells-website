# Real Estate Star Pitch Decks — Design Spec

**Date:** 2026-03-31
**Author:** Eddie Rosado
**Status:** Draft

---

## Context

Real Estate Star has evolved from a website generator into a discovery-first AI platform that automates real estate agent workflows — website generation, CMA automation, lead capture, and legal compliance — all for $14.99/mo. The product is live with one active agent and 12 test accounts.

The next step is customer acquisition through a **channel sales model**: recruit salespeople (resellers) who sell Real Estate Star to agents and brokerages, keeping a setup fee ($49–$149) as their commission. Two pitch decks are needed:

1. **Reseller Recruitment Deck** — convinces salespeople to resell the product
2. **Agent Sales Deck** — what resellers present to agents to close deals

Both decks are delivered as **markdown content** (for PowerPoint conversion) and **HTML presentations** (for immediate browser-based use, hostable on Cloudflare Pages).

---

## Market Context

### The Opportunity

- **1.2M+ active NAR members**, 2–3M total licensed agents in the US
- **$12.3B** real estate SaaS market (2026), growing to **$38.3B by 2033** (12.9% CAGR)
- **54% of agents** spend $50–$500/mo on tech tools
- **68% of agents** already use AI tools (NAR 2025 Tech Survey)
- **Median new agent income: $8,100/yr** — most can't afford $300–$500/mo platforms

### The Gap

No platform combines website + CMA + lead automation + compliance at under $100/mo for solo agents. The market is split:

- **Premium tier** ($300–$1,700/mo): KVCore, Ylopo, Chime, BoomTown — overkill and overpriced for solo agents
- **Budget tier** ($40–$150/mo): Placester, iNCOM — basic websites, no CMA, minimal automation
- **CMA-only tools** ($40–$99/mo): Cloud CMA, Saleswise, PropStream — require separate subscriptions

Real Estate Star fills the gap: **all features, $14.99/mo, zero setup friction**.

### Competitive Landscape

| Platform | Monthly | Setup Fee | Website | CMA | Lead Auto | Compliance | AI-Native |
|----------|---------|-----------|---------|-----|-----------|------------|-----------|
| **Real Estate Star** | **$14.99** | **$0** | **Yes** | **Yes** | **Yes** | **Yes** | **Yes** |
| KVCore / BoldTrail | ~$499 | $500–$1,000 | Yes | Add-on | Yes | No | Partial |
| Ylopo | Custom | Unknown | Yes | No | Yes | No | Yes |
| Chime / Lofty | ~$449 | Unknown | Yes | No | Yes | No | Partial |
| Real Geeks | $249–$399 | $250 | Yes | No | Partial | No | No |
| Placester | $59–$329 | $0 | Yes | No | No | No | No |
| iNCOM | ~$40 CAD | $250 CAD | Yes | No | Partial | No | No |
| AgentFire | One-time | $700–$3,500 | Yes | No | No | No | No |
| Luxury Presence | $300–$1,500 | $500–$6,000 | Yes | No | No | No | Partial |
| Cloud CMA (standalone) | ~$40 | $0 | No | Yes | No | No | No |

**Key takeaway:** Real Estate Star is the only platform that does everything at under $100/mo.

---

## Reseller Economics

### Setup Fee Model

Resellers charge agents a one-time setup fee and keep 100% of it:

| Tier | Setup Fee Range | Who It's For |
|------|----------------|-------------|
| **Individual Agent** | $49–$149 | Solo agents, new licensees |
| **Brokerage** | Per-agent pricing (volume discount) | Brokerages onboarding multiple agents |

Suggested brokerage pricing:

| Agents | Setup Fee Per Agent | Reseller Total |
|--------|-------------------|----------------|
| 1–5 | $99–$149 | $99–$745 |
| 6–15 | $79–$99 | $474–$1,485 |
| 16–50 | $49–$79 | $784–$3,950 |
| 50+ | Custom / negotiated | Scales with deal size |

### Why This Sells

- **Agent's monthly cost:** $14.99/mo — less than a lunch out
- **Reseller's per-sale earnings:** $49–$149 per agent, $500–$4,000+ per brokerage deal
- **No ongoing obligation for the reseller** — the product retains itself through value
- **Competitor setup fees are $250–$6,000** — resellers can honestly say "we're cheaper on setup AND monthly"

### Competitive Setup Fee Context (What Agents Are Used To Paying)

| Competitor | Setup Fee |
|-----------|-----------|
| KVCore | $500–$1,000 |
| Luxury Presence | $500–$6,000 |
| AgentFire | $700–$3,500 |
| Real Geeks | $250 |
| iNCOM | $250 CAD |
| **Real Estate Star (via reseller)** | **$49–$149** |

---

## Deck 1: Reseller Recruitment Deck

**Audience:** Salespeople, tech consultants, real estate coaches, brokerage admins
**Goal:** Convince them to sell Real Estate Star to agents
**Tone:** Opportunity-focused, data-driven, professional

### Slide 1 — Cover

**Real Estate Star**
*Sell the Future of Real Estate Tech*

Subtitle: A reseller opportunity in real estate's most underserved market

### Slide 2 — The Opportunity

**1.2 million agents. Most priced out of good tech.**

- $12.3B real estate SaaS market, growing 12.9% annually
- 54% of agents spend $50–$500/mo on tech — but get fragmented, overpriced tools
- Median new agent income: $8,100/yr — they can't afford $300–$500/mo platforms
- 68% already use AI — appetite is there, affordable supply isn't

### Slide 3 — The Gap in the Market

**No platform combines website + CMA + lead automation + compliance under $100/mo.**

Visual: Market map showing premium tier ($300+), budget tier ($40–$150), and the empty space at $15/mo with full features.

- Premium platforms (KVCore, Ylopo, Chime): $300–$1,700/mo — built for teams, not solo agents
- Budget options (Placester, iNCOM): $40–$150/mo — basic websites, no CMA, no automation
- CMA tools (Cloud CMA, Saleswise): $40–$99/mo — separate subscription on top of everything else
- **Real Estate Star: $14.99/mo — everything included**

### Slide 4 — Meet Real Estate Star

**Everything an agent needs. One platform. $14.99/mo.**

- Professional white-label website (branded, mobile-ready, compliant)
- AI-powered CMA automation (lead in, branded PDF out)
- Lead capture with bot protection and auto-enrichment
- State-specific legal compliance (disclosures, consent tracking)
- Google Workspace integration (agent's own Drive, Gmail, Sheets)
- 10-minute AI-powered setup (connect Google, AI discovers your brand)

### Slide 5 — Why It Sells Itself

**You're selling a $15/month product that replaces $300–$500/month of tools.**

| What Agents Pay Now | Real Estate Star |
|---|---|
| Website: $59–$329/mo | Included |
| CMA tool: $40–$99/mo | Included |
| Lead capture: $50–$100/mo | Included |
| Compliance: manual / risky | Included |
| **Total: $149–$528+/mo** | **$14.99/mo** |

"This isn't a hard sell. It's a math problem."

### Slide 6 — Your Economics

**You keep 100% of the setup fee. Every sale.**

| Deal Type | Your Setup Fee | Agent's Monthly | Your Earnings |
|----------|---------------|----------------|--------------|
| Solo agent | $49–$149 | $14.99/mo | $49–$149 per sale |
| Small brokerage (10 agents) | $79–$99/agent | $14.99/mo/agent | $790–$990 per deal |
| Mid brokerage (25 agents) | $49–$79/agent | $14.99/mo/agent | $1,225–$1,975 per deal |
| Large brokerage (50+ agents) | Negotiated | $14.99/mo/agent | $2,450+ per deal |

- **10 solo agents/month** = $490–$1,490/mo in setup fees
- **1 brokerage deal/month (25 agents)** = $1,225–$1,975 per deal
- **No cap on earnings. No territory restrictions.**

### Slide 7 — Competitive Landscape

Full comparison table (from Market Context section above).

Callout: "Your pitch to agents is simple: 'You're paying $300–$500/month. This does the same thing for $15. And I'll set it up for you.'"

### Slide 8 — The Product (Visual Overview)

Screenshots / feature highlights:
- Agent website (white-label, branded)
- CMA report (AI-generated, branded PDF)
- Lead form (with Turnstile bot protection)
- Activation flow (connect Google → AI builds your profile)

### Slide 9 — How Activation Works

**Agent connects Google. AI does the rest.**

```
1. Agent clicks your setup link
2. Google OAuth — connects Gmail, Drive
3. AI scans their existing data (emails, contacts, listings)
4. AI extracts: brand colors, voice, personality, headshot
5. Website goes live at {handle}.real-estate-star.com
6. CMA pipeline activated — ready for leads
7. Total time: ~10 minutes
```

"You don't need to build anything. You don't need to configure anything. The AI does the setup."

### Slide 10 — What Agents Get

| Feature | Description |
|---------|-------------|
| Professional Website | 12 luxurious templates, white-label, branded, mobile-ready. Custom looks available. |
| SEO & AEO | Optimized for Google search AND AI answer engines — agents get found |
| Google Analytics | Built-in visitor and lead tracking |
| CMA Automation | Lead submits form → AI generates branded CMA PDF → emailed automatically |
| Lead Capture | Website forms with bot protection, auto-enrichment, instant notifications |
| Legal Compliance | State-specific disclosures, consent tracking, GDPR/CCPA/CAN-SPAM ready |
| Google Integration | Everything lives in the agent's Google Drive — they own their data |
| AI Brand Discovery | AI reads their emails and web presence to extract their unique voice and brand |

### Slide 11 — Brokerage Deals

**Volume pricing for brokerages — your biggest earning opportunity.**

- Brokerages want uniform tech across their agents
- One conversation → 10–50+ agent signups
- You negotiate the setup fee per agent (suggested: $49–$99 for volume)
- Monthly stays $14.99/agent — no brokerage markup from us

"Position it as: 'Give your agents professional websites and automation for less than their monthly coffee budget.'"

### Slide 12 — Support & Resources

**What we provide to resellers:**

- Agent Sales Deck (ready to present)
- Setup link generation (you send the link, agent clicks, AI does the rest)
- Product training materials
- Dedicated reseller support channel
- Feature roadmap visibility (auto-replies, contract drafting, MLS coming soon)

**Your commitment to the agent:**

- Follow through on customer satisfaction — if the agent isn't happy with their website, you work with us to make it right
- 12 templates cover most agents, but if they need a custom look or have an existing site, we accommodate (may take additional time)
- Your reputation is tied to the product — we make sure you look good

### Slide 13 — Get Started

**Ready to start selling?**

- Contact: [Eddie Rosado / Real Estate Star contact info]
- No upfront cost to become a reseller
- Start earning on your first sale

---

## Deck 2: Agent Sales Deck

**Audience:** Real estate agents (solo practitioners, small teams)
**Goal:** Convince agents to sign up for Real Estate Star
**Tone:** Empathetic, benefit-focused, simple
**Presented by:** Resellers (or directly by Real Estate Star)

### Slide 1 — Cover

**Real Estate Star**
*Your business, automated by AI.*

Subtitle: Professional website. CMA automation. Lead capture. Compliance. All for $14.99/mo.

### Slide 2 — The Problem

**You're paying too much for tools that don't talk to each other.**

- Website builder: $59–$329/mo
- CMA tool: $40–$99/mo
- Lead capture: $50–$100/mo
- Compliance: "I'll figure it out later" (risky)
- **Total: $149–$528/mo — and none of them integrate**

"What if one platform did all of this for $14.99?"

### Slide 3 — The Real Cost

**What top platforms charge solo agents:**

| Platform | Monthly Cost | Setup Fee |
|----------|-------------|-----------|
| KVCore / BoldTrail | ~$499/mo | $500–$1,000 |
| Chime / Lofty | ~$449/mo | Unknown |
| Real Geeks | $249–$399/mo | $250 |
| Luxury Presence | $300–$1,500/mo | $500–$6,000 |
| Placester | $59–$329/mo | $0–$50/mo |
| **Real Estate Star** | **$14.99/mo** | **Setup by your reseller** |

"These platforms were built for teams and brokerages. You shouldn't be paying enterprise prices."

### Slide 4 — Meet Real Estate Star

**Everything you need. Nothing you don't.**

- Professional branded website at {yourname}.real-estate-star.com
- AI-powered CMA reports — lead submits, PDF arrives in their inbox
- Lead capture forms with bot protection and instant notifications
- State-specific legal compliance built in
- Your data stays in YOUR Google Drive — you own everything
- Setup takes 10 minutes, not 2–4 weeks

### Slide 5 — How It Works

**Connect Google. AI does the rest.**

```
Step 1: Click the setup link
Step 2: Connect your Google account (Gmail, Drive)
Step 3: AI scans your emails and web presence
Step 4: AI builds your brand profile (colors, voice, headshot)
Step 5: Your website goes live — ready for leads
```

"No forms to fill out. No branding questionnaires. No waiting weeks for a designer. The AI already knows who you are from your digital footprint."

### Slide 6 — Feature: Professional Website

**Your brand. Your site. Live in minutes.**

- 12 beautiful, luxurious templates to choose from
- White-label design branded to you (colors, logo, headshot)
- Mobile-responsive, fast-loading (Cloudflare edge network)
- SEO and AEO (Answer Engine Optimization) ready — found by Google and AI search
- Google Analytics integration — track your visitors and leads
- About page, testimonials, service areas — all auto-populated from your profile
- Have an existing site or want a custom look? We can accommodate — just takes a bit more time

### Slide 7 — Feature: CMA Automation

**A lead asks about their home's value. The CMA arrives before you pick up the phone.**

- Lead submits property address on your website
- AI pulls comparable sales data (RentCast — real MLS-grade data)
- AI generates a branded PDF report with your colors, logo, and contact info
- PDF is emailed to the lead AND saved to your Google Drive
- **Fully automated. No manual work.**

"Your competitors are spending 30–60 minutes per CMA. Yours takes seconds."

### Slide 8 — Feature: Lead Capture & Management

**Every lead captured. Every lead enriched. Every lead notified.**

- Lead forms on your website (buyer and seller)
- Cloudflare Turnstile bot protection (no fake leads)
- AI enrichment — automatically researches the lead and their property
- Instant notification to you (email or WhatsApp)
- All lead data organized in your Google Drive
- Consent tracking and opt-out management built in

### Slide 9 — Feature: Legal Compliance

**Built-in. Not bolted on.**

- State-specific disclosure requirements (lead-based paint, property condition, etc.)
- Consent tracking (TCPA, CAN-SPAM, GDPR/CCPA compliant)
- Equal housing notice
- Cookie consent
- Fair housing compliance
- "Your competitors make you figure this out yourself. We handle it."

### Slide 10 — Pricing

**$14.99/mo. Everything included. No add-ons. No surprises.**

| | Real Estate Star | Typical Competitor Stack |
|---|---|---|
| Website | Included | $59–$329/mo |
| CMA Tool | Included | $40–$99/mo |
| Lead Capture | Included | $50–$100/mo |
| Compliance | Included | DIY (risky) |
| AI Automation | Included | $100–$200/mo add-on |
| **Total** | **$14.99/mo** | **$249–$728/mo** |

"That's less than your monthly Netflix subscription."

### Slide 11 — What's Coming Next

**The platform grows with you:**

- Auto-Replies — AI-powered instant lead response in your voice
- Contract Drafting — state-specific contracts, auto-filled from lead data
- DocuSign Integration — send contracts for signature directly
- MLS Automation — list properties without re-entering data
- Photographer Scheduling — coordinate listing photos automatically

"You're getting in early. These features are included at $14.99/mo — no price increases for early adopters."

### Slide 12 — Get Started

**14-day free trial. No credit card required.**

1. Your reseller sends you a setup link
2. Connect your Google account (10 minutes)
3. AI builds your profile and website
4. Start capturing leads immediately

"The only thing you have to lose is the $300/mo you're spending now."

---

## Deliverables

| Deliverable | Format | Purpose |
|-------------|--------|---------|
| `docs/pitch-decks/reseller-deck.md` | Markdown | Content source for reseller PowerPoint deck |
| `docs/pitch-decks/agent-deck.md` | Markdown | Content source for agent PowerPoint deck |
| `docs/pitch-decks/reseller-deck.html` | HTML | Browser-based reseller presentation |
| `docs/pitch-decks/agent-deck.html` | HTML | Browser-based agent presentation |

### HTML Presentation Requirements

- Single-file HTML (no external dependencies)
- Keyboard navigation (arrow keys)
- Real Estate Star branding (colors from existing agent-site config)
- Print-friendly (for PDF export)
- Responsive (works on laptop/tablet screens)
- Professional slide transitions

### Branding

Pull from existing Real Estate Star branding:
- Primary color: from platform/agent-site CSS variables
- Logo: existing Real Estate Star logo (if available) or text-based
- Font: match existing platform typography
- Tone: professional, confident, data-driven

---

## Verification

- [ ] All competitor pricing data has sources (from market research)
- [ ] Setup fee ranges align with competitive benchmarks
- [ ] TAM and market size numbers are cited
- [ ] Both decks tell a complete narrative without requiring the other
- [ ] HTML presentations render correctly in Chrome/Edge/Safari
- [ ] Markdown files are structured for easy PowerPoint conversion (one section per slide)
- [ ] No hardcoded agent data (generic examples, not Jenise-specific)
- [ ] "Coming soon" features match actual roadmap items from CLAUDE.md

---

## Sources

All competitive data sourced from market research conducted 2026-03-31. Key sources:
- NAR 2025 Member Profile & Technology Survey
- The Close (platform reviews and pricing)
- Coherent Market Insights / Grand View Research (market size)
- Rewardful (SaaS affiliate benchmarks)
- Platform-specific pricing pages (KVCore, Placester, Real Geeks, etc.)

Full source list available in research file.
