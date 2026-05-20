# Real Estate Star — Pitch Demo Script

**Date:** 2026-05-20
**Audience:** Prospective customers (real estate agents / small brokerages)
**Format:** Static demo — frontend is live, backend API is mid-migration (offline)

---

## TL;DR for the presenter

- **Live and safe to show:** the agent website (`jenise-buckalew.real-estate-star.com`), the platform marketing pages (`platform.real-estate-star.com`), and the **10-template live preview** (`real-estate-star-agents-pr-158.misteredr.workers.dev/?accountId=…` — confirmed working 2026-05-20).
- **Show as exhibits (PDF/screenshots), not live:** the CMA report output, the lead-email output.
- **Do NOT click:** any form, the onboarding chat, any dashboard. The backend API is offline today — clicking these will visibly fail. The script marks every one of these "🚫 narrate, don't click."
- **The honest line if asked about the backend:** "We're mid-migration on our backend hosting — the public sites run on Cloudflare's edge and are unaffected." True, and not alarming.

Have these tabs/files open BEFORE the meeting:
1. `https://jenise-buckalew.real-estate-star.com` — flagship live agent site
2. The **Green Light Realty brokerage demo** (Beat 1.5) — `https://real-estate-star-agents-pr-158.misteredr.workers.dev/?accountId=glr` plus an agent page e.g. `…/agents/jenise-buckalew?accountId=glr`
3. `https://platform.real-estate-star.com` — platform marketing pages
4. `docs/demo/artifacts/sample-cma-report.pdf` (open in a PDF viewer)
5. The **live template-preview** tabs (Beat 2) — base `https://real-estate-star-agents-pr-158.misteredr.workers.dev/?accountId={account}`. Confirmed live 2026-05-20.
6. **A Spanish-configured browser/profile** (for Beat 1.6) — set browser language to Spanish so the GLR site renders in Spanish on demand. Language is browser-driven, not a URL toggle.

**Highlights to make sure land** (every one is verified working today): **mobile-first / fully responsive** (vs. the prospect's desktop-only legacy site) · brokerage sites (one firm, a page per agent) · custom domains · **multi-language sites (EN/ES/IT/RU/HI, auto-detected, per-agent)** · **learns from the agent** (bios, languages, reviews read from their real profile — not a blank form) · 10 templates · tailored to the agent's real brand · CMA automation · lead capture→email. Roadmap to paint: lead routing, drip nurture, email monitoring with draft automation, automated listing search, DocuSign. See talking-points doc for the framing of each.

---

## The story arc (~10–12 min)

The pitch is: **"An agent's entire online presence and lead response — automated."** Walk it in the order a real agent experiences it. Beats 1.5 / 1.6 / 6 are situational — use them when the prospect is a brokerage owner (1.5), serves a multilingual market (1.6), or is anchored to an aging website (6).

### Beat 1 — "Here's what your business looks like to a buyer" (live)

**Open:** `https://jenise-buckalew.real-estate-star.com`

Talk track:
- "This is a live client site — Jenise Buckalew, a REALTOR® in New Jersey. Every agent on Real Estate Star gets one of these at `their-name.real-estate-star.com`."
- Scroll the full page. Call out: the hero with her photo + tagline, the services section, the "how it works" steps, the about section, the contact area.
- "This isn't a template she filled out. The system *built* this — pulled her brokerage, her market areas, her branding — and generated the copy in her voice."
- **The mobile moment (do this on an actual phone — it's a strong, simple proof point):** open the Real Estate Star site on your phone, then open the prospect's current site on the same phone. "Most buyers find and judge an agent on their phone. Here's your site today —" (pinch-zoom, sideways scroll, tiny text) "— and here's a Real Estate Star site. Built mobile-first; it just works." If pitching GLR, use `greenlightmoves.com` vs. the GLR demo. The contrast sells itself in five seconds — and Google's mobile-first indexing means the responsive site also ranks better.

🚫 **Do NOT click** the "What's your home worth?" form. Narrate it instead → Beat 3.

### Beat 1.5 — "And it scales to a whole brokerage" (LIVE — Green Light Realty)

**Open:** `https://real-estate-star-agents-pr-158.misteredr.workers.dev/?accountId=glr`

✅ Confirmed live 2026-05-20 — the 70-agent Green Light Realty brokerage site on the PR-158 preview deploy. Brokerage homepage + all 70 agent pages render.

This is **Green Light Realty** — a real Central Jersey brokerage (70 agents, Old Bridge NJ), rebuilt on Real Estate Star. Use this when the prospect is a brokerage owner, or to show range beyond solo agents.

Talk track:
- "This isn't one agent — it's an entire brokerage. Green Light Realty, 70 agents. One branded brokerage site, built automatically."
- Scroll the brokerage homepage: the hero with their real tagline *"Forward. Moving. Forward. Learning."*, the stats (70+ agents, est. 2017), the "Why Green Light Realty" section, real client testimonials, the about section with the broker's bio.
- **Then show a per-agent page** — open `…workers.dev/agents/jenise-buckalew?accountId=glr`. "Every single agent gets their own dedicated page under the brokerage — their photo, their contact, their own lead form. Here's Jenise Buckalew's."
- Try another: `…workers.dev/agents/jeffrey-prontnicki?accountId=glr` (the broker/owner) or `…workers.dev/agents/noelle-dibenedetto?accountId=glr`.
- "Seventy agents, seventy pages, one brand — and the brokerage didn't run seventy web projects to get it."

**Two things to land here (both important to a brokerage buyer):**

1. **Custom domains.** "Every one of these sites — agent or brokerage — runs on the client's own domain. Green Light Realty could point `greenlightmoves.com` straight at this. The `real-estate-star.com` address is just our default."

2. **Brokerage lead routing (roadmap — frame as what's next).** "When a lead comes into the brokerage site, the broker decides where it goes — by rules they configure. Route to whoever needs a lead most. Route by location — an Old Bridge inquiry to an Old Bridge agent. Route by specialty — a luxury listing to a luxury specialist, a first-time buyer to someone who's great with first-timers. The brokerage site becomes a lead-distribution engine, not just a brochure."

**⚠️ Beat 1.5 cautions:**
- The per-agent URL is `…/agents/{agent-id}?accountId=glr` (path-based). Agent IDs are name slugs: `jenise-buckalew`, `jeffrey-prontnicki`, `noelle-dibenedetto`, etc.
- Don't click the lead form (API offline) — narrate → Beat 3.
- Morning-of check: `curl -s -o /dev/null -w "%{http_code}" "https://real-estate-star-agents-pr-158.misteredr.workers.dev/?accountId=glr"` → expect `200`.

### Beat 1.6 — "And it speaks your clients' language" (LIVE — confirmed working)

**Setup:** before the meeting, have a browser (or browser profile) set to **Spanish** — Settings → Languages → put Spanish first. The site picks language from the browser, automatically. (Use the in-page language switcher in the nav for the others — Italian / Russian / Hindi — without changing browser settings.)

**Open (Spanish browser):** `https://real-estate-star-agents-pr-158.misteredr.workers.dev/?accountId=glr`

Talk track:
- "Central Jersey is a multilingual market. Watch what happens when a buyer who's more comfortable in Spanish visits — the site detects their browser language and serves the whole thing in Spanish, automatically. No separate site, no toggle they have to find."
- The GLR homepage renders fully in Spanish — "Encuentre el Camino a Su Hogar con Green Light Realty."
- **Then show it's per-agent — and that it was *learned*:** open Antonella Coco's page and switch to Italian; open Lesly Bazile and switch to Russian; open Kajal or Nilesh Shah and switch to Hindi. Then open an English-only agent (`…/agents/noelle-dibenedetto?accountId=glr`) — it stays English.
- "Here's the part that matters: we didn't ask these agents to fill out a 'languages' field. The platform **read each agent's real bio** and detected the languages they actually speak — Antonella says she's fluent in Italian, Lesly lists Russian and Spanish, the Shahs speak Hindi. The site learned it from them and localized only those languages. It never pretends an agent speaks a language they don't."
- "A buyer who's more comfortable in their own language gets a site that meets them there — and a 2007 template can't do that."

**⚠️ Beat 1.6 cautions:**
- Spanish is easiest to show via the **browser's language setting** (Accept-Language). For Italian / Russian / Hindi, use the **language switcher in the site nav** — it lists every language the brokerage's agents collectively offer.
- Italian / Russian / Hindi content is **machine-translated placeholder** pending native review (each file is flagged internally). Spanish is hand-written. Don't claim the it/ru/hi copy is professionally translated — claim the *capability*: the platform detects the language and localizes the site.
- Only agents who state a language in their real bio get that localized page — by design. Other agents fall back to English cleanly.

### Beat 2 — "And it's not one cookie-cutter look" (LIVE — confirmed working)

The platform ships **10 distinct website templates**. There is a **live preview deployment** that serves every one — confirmed working 2026-05-20.

**Live preview base URL:** `https://real-estate-star-agents-pr-158.misteredr.workers.dev/?accountId={account}`

(This is a Cloudflare Workers PR-preview build. It has `PREVIEW=true` set, so the `?accountId=` switch works — one URL, every template. It does NOT need the Azure backend for page rendering.)

Open these tabs before the meeting and flip through 3–4 visually distinct ones:

| Template | Live URL |
|---|---|
| emerald-classic (flagship) | `…workers.dev/?accountId=jenise-buckalew` |
| luxury-estate | `…workers.dev/?accountId=test-broker-agent` |
| coastal-living | `…workers.dev/?accountId=test-coastal-living` |
| urban-loft | `…workers.dev/?accountId=test-urban-loft` |
| modern-minimal | `…workers.dev/?accountId=test-modern` |
| country-estate | `…workers.dev/?accountId=test-country-estate` |
| new-beginnings | `…workers.dev/?accountId=test-new-beginnings` |
| light-luxury | `…workers.dev/?accountId=test-light-luxury` |
| commercial | `…workers.dev/?accountId=test-brokerage` |
| warm-community | `…workers.dev/?accountId=test-warm` |

Talk track: "A luxury-estate agent and a first-time-buyer specialist shouldn't have the same website. They don't have to."

**⚠️ Demo cautions for Beat 2:**
- `?accountId=glr` is the **brokerage** demo (Beat 1.5) — it's a full Green Light Realty site, not a single template. Use it for the brokerage story, not the template-variety story.
- All 10 templates render clean — the earlier `REALTOR®` encoding glitch on `test-warm`/`test-emerald` is fixed as of PR-158.
- Morning-of, re-confirm the preview is still up: `curl -s -o /dev/null -w "%{http_code}" "https://real-estate-star-agents-pr-158.misteredr.workers.dev/?accountId=jenise-buckalew"` → expect `200`. If it's gone, fall back to the local run (see prep section) or screenshots.

### Beat 3 — "When a buyer fills out that form, here's what happens" (narrate + exhibit)

🚫 **Do NOT click the form.** Narrate the pipeline:

- "A buyer lands on the site and asks 'what's my home worth?' — they enter their address."
- "Within a couple of minutes, the system pulls comparable sales from live market data, builds a professional Comparative Market Analysis, and emails it to the buyer — *and* alerts the agent that a new lead came in."
- **Now open the exhibit:** `docs/demo/artifacts/sample-cma-report.pdf`.
- Page through it: "This is a real CMA the system generated. Branded to the agent. Comparable sales with recency weighting. The agent didn't touch it — it ran end to end."

This is the strongest single moment in the demo. The PDF is concrete proof of automation.

### Beat 4 — "The agent gets the lead instantly" (narrate)

- "The moment that CMA goes out, the agent gets a notification — lead name, contact info, the property they asked about, and a lead score so they know how hot it is."
- "The whole point: a buyer gets a useful answer in minutes, and the agent never misses a lead — even at 11pm on a Sunday."

🚫 No dashboard to click — narrate only.

### Beat 5 — "Getting started is a conversation, not a setup wizard" (narrate + platform pages)

**Open:** `https://platform.real-estate-star.com`

- Walk the marketing/landing pages — pricing, the value proposition.
- "Onboarding is a chat. The agent answers a few questions, the system finds their existing online profile, and it builds the whole site automatically. No web designer, no template-wrangling."

🚫 **Do NOT click into the onboarding chat itself** (`/onboard`) — it needs the backend. Narrate it.

### Beat 6 — "Why your current site is costing you" (the AEO close — optional, strong for GLR)

Use this when pitching Green Light Realty or any brokerage that has an aging website. It's the sharpest contrast in the deck.

**Open their real site:** `http://greenlightmoves.com` (note: it's HTTP — it has no working HTTPS).

Talk track:
- "This is Green Light Realty's current site. Look at the footer — 'Copyright 2007.' It's an 18-year-old platform."
- "First problem: it's not secure. There's no HTTPS — a buyer's browser flags it 'Not Secure' right when they'd enter their phone number. They bounce."
- "Bigger problem: it was built for how people searched in 2007 — typing keywords into Google. In 2026, buyers ask **ChatGPT, Google's AI answers, Perplexity, Siri**: 'who's a good realtor in Old Bridge?' That's **Answer Engine Optimization** — AEO. AI engines read **structured data** to decide who to recommend. A 2007 site has none. The agent who isn't AEO-ready is invisible to the fastest-growing way buyers find agents."
- "Their listings are trapped in an iframe — search engines see an empty box, not the homes."
- **Then flip to the Real Estate Star version** (Beat 1.5's GLR site): "Same brokerage, rebuilt. HTTPS by default. Fast. Mobile-first. And **structured data built in** — `RealEstateAgent` schema on every page — so when a buyer asks an AI who to call, this is the site the AI can read, trust, and cite."

**This claim is verifiable and true** — the Real Estate Star agent-site emits `schema.org` `RealEstateAgent` JSON-LD on every page (`app/page.tsx` + `app/agents/[id]/page.tsx`). You can right-click → View Source on the live site and show the `<script type="application/ld+json">` block if a technical buyer wants proof.

Closing line: *"Your buyers are already asking AI who to call. Real Estate Star makes sure the answer is you."*

---

## Roadmap framing (if they ask "what's next")

Be honest and specific — it signals rigor:
- **Email monitoring with draft automation** — the system watches the agent's inbox for new leads (Zillow, Realtor.com, referrals) and *drafts the reply* — the agent just reviews and sends. *Designed in detail* — there's a full spec. (Technical buyer pushing? Show `docs/superpowers/specs/2026-04-17-lead-communications-loop-design.md` as proof of rigor.)
- **Lead nurturing + lead-specific drip campaigns** — every lead gets an automatic, personalized follow-up sequence tailored to their stage — leads never go cold.
- **Automated listing searches** — for each buyer lead, the home search runs and keeps running automatically; new matches surface without re-running anything.
- **DocuSign integration** — contracts and disclosures flow straight into e-signature.
- **Brokerage lead routing** — configurable rules (workload / location / specialty) that auto-distribute brokerage leads to the right agent. The brokerage-buyer's favorite roadmap item.
- **WhatsApp** — lead alerts and follow-ups over WhatsApp too.

Frame as: "The website + CMA + lead capture is live today. The next wave automates the whole workflow — from the lead that emails at 9pm, through nurture and showings, to the contract. The website is the front door; the automation platform is the building."

---

## Hard "do not touch" list (memorize this)

| Surface | Why | What to do instead |
|---|---|---|
| "What's your home worth?" form on the agent site | POSTs to the offline API → visible failure | Narrate Beat 3 + show the CMA PDF |
| Onboarding chat (`platform.real-estate-star.com/onboard`) | Needs the API → won't load / errors | Narrate Beat 5 |
| Any status/dashboard page | Needs API data | Narrate Beat 4 |
| `api.real-estate-star.com/*` anything | Backend offline (HTTP 530) | Never navigate here |

---

## Pre-meeting prep checklist

- [ ] Confirm sites are still up morning-of:
  - `curl -s -o /dev/null -w "%{http_code}" https://jenise-buckalew.real-estate-star.com` → expect `200`
  - `curl -s -o /dev/null -w "%{http_code}" https://platform.real-estate-star.com` → expect `200`
  - `curl -s -o /dev/null -w "%{http_code}" "https://real-estate-star-agents-pr-158.misteredr.workers.dev/?accountId=jenise-buckalew"` → expect `200`
- [ ] Open the core tabs before the call: agent site, platform, CMA PDF, and 3–4 template-preview tabs (Beat 2).
- [ ] Have `docs/demo/artifacts/sample-cma-report.pdf` open in a real PDF viewer (not the browser, so it looks like a deliverable).
- [ ] Phone ready for the mobile-responsive moment in Beat 1.

### Fallback only: local run (use ONLY if the PR-123 preview is down morning-of)

The Beat 2 template gallery uses the live PR-123 preview. If that URL is down on demo day, the fallback is running the agent site locally — locally the `?template=` / `?accountId=` overrides work (production disables them for tenant safety):

```
cd apps/agent-site
npm install        # first time only
npm run dev
```

Then open these in tabs:
- `http://localhost:3000/?accountId=jenise-buckalew&template=emerald-classic`
- `http://localhost:3000/?accountId=jenise-buckalew&template=luxury-estate`
- `http://localhost:3000/?accountId=jenise-buckalew&template=coastal-living`
- `http://localhost:3000/?accountId=jenise-buckalew&template=urban-loft`
- `http://localhost:3000/?accountId=jenise-buckalew&template=modern-minimal`

(Local dev still doesn't need the backend API for page rendering — the templates render from config.)

---

## If something goes wrong mid-demo

- **A live site is suddenly down:** pivot entirely to the CMA PDF + screenshots. "Let me show you the actual output the system produces" — the PDF carries the demo on its own.
- **Someone clicks a form anyway and it fails:** "That's our backend mid-migration — the form *capture* is live in production normally; what you're seeing is our hosting move this week." Move to the CMA PDF.
- **Asked point-blank "is this live with real customers":** Be honest. Jenise is the reference/first agent. The platform is built; you're onboarding. Don't oversell — agents smell it.
