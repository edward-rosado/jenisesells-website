# Product Plan — Real Estate Star (Rewritten)

## Mission Statement

**Real Estate Star** is a white-label SaaS platform that empowers real estate agents to close more deals with AI-powered marketing tools. Agents get their own branded microsite, instant CMA reports, and professional lead capture — all designed to convert leads into clients.

---

## Product Modules (5 Total)

```
real-estate-star/
├── apps/
│   ├── platform/              # SaaS admin & customer portal (real-estate-star.com)
│   │   ├── app/[...]/         # Landing, pricing, auth, billing pages
│   │   ├── components/ui/     # Design system components
│   │   └── lib/auth/          # Authentication & authorization
│   └── site/                  # Agent microsite (public-facing)
│       ├── app/[agentId]/     # Per-agent branded pages
│       ├── components/ui/     # Design system components
│       └── lib/auth/          # Agent authentication
├── packages/
│   ├── cma/                   # CMA generation engine
│   │   ├── src/comps/         # Comparable sales analysis logic
│   │   ├── src/pdf/           # PDF report generation (QuestPDF)
│   │   ├── src/html/          # HTML report rendering
│   │   └── src/models/        # Data models and validation
│   └── legal/                 # Legal & compliance components
│       ├── src/               # Cookie consent, EHO, disclaimers
│       └── stories/           # Storybook stories
└── docs/                      # Architecture and product documentation
```

### Module Descriptions

| Module | Domain | Purpose |
|--------|---------|---------|
| `apps/platform` | real-estate-star.com | Customer-facing SaaS platform: landing page, pricing, signup, billing, site creation wizard |
| `apps/site` | *.real-estate-star.com or custom domain | Per-agent branded microsite: CMA requests, agent profile, lead capture |
| `packages/cma` | — | CMA generation engine (comps analysis → PDF/HTML reports) |
| `packages/legal` | — | Reusable legal/compliance React components |

**Removed from scope:** Contract drafting (`packages/contracts`) — not part of MVP.

---

## Target Users

| User | Description |
|------|-------------|
| **Individual Agent** | Solo agent wanting a polished online presence and lead conversion tools |
| **Brokerage Team** | Team of agents sharing brokerage branding with individual customization |

## Core Value Propositions

1. **"Your Personal AI Marketing Department"** — Automated CMA reports and professional lead capture
2. **"Close More Deals, Faster"** — Professional documents that build trust with sellers
3. **"White-Label Ready"** — Your brand, your rules, your clients

---

## Product Roadmap

### Phase 1: Foundation (Week 1) — ✅ Complete
- [x] Define product scope and architecture
- [x] Document technical design
- [x] Set up monorepo structure with focused modules
- [x] Create agent microsite shell (Next.js + Tailwind)
- [x] Create CMA package skeleton (QuestPDF, Zod validation)
- [x] Create legal compliance components (cookie consent, EHO)
- [x] Set up CI/CD pipeline
- [x] Containerize application with Docker
- [x] Add monitoring and logging (Sentry)
- [x] Write comprehensive documentation

### Phase 2: Core Features (Week 2)
- [ ] Implement CMA generation engine (comps analysis → PDF + HTML reports)
- [ ] Build platform landing page and pricing section
- [ ] Set up email integration using agent/brokerage personal accounts
- [ ] Add Google Places autocomplete for property addresses

### Phase 3: White-Label & Multi-Tenancy (Week 3)
- [ ] Custom domain support per agent/brokerage
- [ ] Tenant-aware CMA PDF generation with branded templates
- [ ] Agent onboarding flow (profile setup, branding config)
- [ ] Site creation wizard (branding setup, domain configuration)
- [ ] Multi-language support expansion

### Phase 4: Platform & Billing (Week 4)
- [ ] Customer dashboard (manage agents, view analytics)
- [ ] Stripe billing integration (subscription plans) — *lowest priority*
- [ ] Lead routing and notification system
- [ ] Security audit and compliance review

### Phase 5: Sales-Ready Polish (Week 5)
- [ ] Documentation site (agent guide + admin guide)
- [ ] Performance optimization pass
- [ ] Marketing copy and SEO polish

---

## Technical Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Framework | Latest Next.js (App Router, latest recommendations) | SSR for SEO, API routes, large ecosystem |
| Monorepo structure | pnpm workspaces | Shared types, atomic commits, unified CI/CD |
| PDF generation | QuestPDF | Type-safe, performant, no system deps |
| HTML reports | Next.js server components | Reusable, accessible, branded web views |
| Database | PostgreSQL (free tier) | No PII stored; GDPR-compliant by design |
| Email | Agent/brokerage personal accounts | More personal feel for leads |
| Auth | Flexible provider support | Must support multi-agent-sites easily |
| Billing | Stripe (last priority) | Subscription management |

### Why Monorepo?
- Shared types between frontend and backend packages
- Atomic commits across all changes
- Unified testing and CI/CD
- Simplified dependency management

### Why Next.js App Router?
- Server-side rendering for SEO (agent sites rank better)
- API routes for backend functionality
- Streaming and partial prerendering for performance
- Large ecosystem of plugins and integrations

### Why QuestPDF for PDFs?
- Type-safe, performant PDF generation in Node.js
- No external dependencies or system libraries needed
- Clean API for complex layouts (tables, images, branding)
- Actively maintained with good documentation

---

## Key Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| CMA Generation Time | < 5 seconds | p95 latency |
| Uptime | > 99.9% | Monitoring dashboard |
| Agent Onboarding Time | < 5 minutes | Time from signup to live site |
| Lead Conversion Rate | > 30% improvement | Before/after comparison |

---

## Competitive Advantages

1. **Speed** — CMA reports in seconds, not hours
2. **Branding** — Every agent gets their own professional microsite
3. **Personal Email** — Leads come from the agent's own account, building trust
4. **Automation** — End-to-end pipeline from lead capture to document delivery
5. **White-Label** — Brokerages can resell under their own brand

---

## Future Enhancements (Post-MVP)

- [ ] MLS integration for automated comp data
- [ ] AI-powered property descriptions
- [ ] Social media content generation
- [ ] CRM integrations (Follow Up Boss, kvCORE, etc.)
- [ ] Video script generator for listing presentations
- [ ] Market insights dashboard with charts and graphs
- [ ] Client portal for document signing and tracking
- [ ] SMS/WhatsApp integration for lead follow-up

---

## File Structure Reference

```
real-estate-star/
├── apps/
│   ├── platform/                    # SaaS admin & customer portal (real-estate-star.com)
│   │   ├── app/                     # Next.js App Router pages
│   │   │   ├── page.tsx             # Landing page / hero section
│   │   │   ├── pricing/page.tsx     # Pricing plans and comparison
│   │   │   ├── auth/                # Sign in / sign up pages
│   │   │   └── dashboard/           # Customer dashboard (post-login)
│   │   │       ├── agents/          # Manage agents
│   │   │       ├── sites/           # View/manage agent microsites
│   │   │       ├── billing/         # Subscription management
│   │   │       └── analytics/       # Usage and performance metrics
│   │   ├── app/api/                 # Platform API routes
│   │   │   ├── auth/                # Auth callbacks
│   │   │   ├── customers/           # Customer CRUD
│   │   │   ├── agents/              # Agent creation & management
│   │   │   └── sites/               # Site provisioning endpoints
│   │   ├── components/
│   │   │   ├── ui/                  # shadcn/ui base components
│   │   │   ├── layout/              # Header, footer, nav
│   │   │   └── platform/            # Platform-specific components
│   │   │       ├── pricing-cards.tsx
│   │   │       ├── agent-list.tsx
│   │   │       └── site-wizard/     # Site creation wizard steps
│   │   ├── lib/
│   │   │   ├── auth/                # Authentication utilities
│   │   │   ├── db/                  # Database client and schema
│   │   │   ├── billing/             # Stripe integration helpers
│   │   │   └── sites/               # Site provisioning logic
│   │   ├── public/                  # Static assets (logos, images)
│   │   ├── tailwind.config.ts
│   │   ├── next.config.js
│   │   ├── package.json
│   │   └── tsconfig.json
│   │
│   └── site/                        # Agent microsite (public-facing)
│       ├── app/[agentId]/           # Per-agent branded pages
│       │   ├── page.tsx             # Home
│       │   ├── about/page.tsx       # About the agent
│       │   ├── cma/request/page.tsx # CMA request form
│       │   └── listings/page.tsx    # Agent listings
│       ├── app/api/                 # API routes
│       │   ├── auth/                # Authentication
│       │   ├── agents/              # Agent profile CRUD
│       │   ├── cma/                 # CMA generation endpoint
│       │   └── leads/               # Lead capture endpoint
│       ├── components/
│       │   ├── ui/                  # shadcn/ui base components
│       │   ├── layout/              # Header, footer, nav
│       │   └── agent/               # Agent-specific components
│       ├── lib/
│       │   ├── auth/                # Authentication utilities
│       │   ├── db/                  # Database client and schema
│       │   └── agents/              # Agent profile helpers
│       ├── public/                  # Static assets (logos, images)
│       ├── tailwind.config.ts
│       ├── next.config.js
│       ├── package.json
│       └── tsconfig.json
│
├── packages/
│   ├── cma/                         # CMA generation engine
│   │   ├── src/
│   │   │   ├── comps/               # Comparable analysis logic
│   │   │   ├── pdf/                 # PDF report generator (QuestPDF)
│   │   │   ├── html/                # HTML report renderer
│   │   │   └── models/              # Data models and validation
│   │   ├── package.json
│   │   └── tsconfig.json
│   └── legal/                       # Legal & compliance components
│       ├── src/
│       │   ├── CookieConsent.tsx
│       │   ├── EqualHousingNotice.tsx
│       │   └── index.ts
│       ├── package.json
│       └── tsconfig.json
│
├── docs/                            # Architecture and product documentation
│   ├── PRODUCT_PLAN.md              # This file
│   ├── ARCHITECTURE.md              # Technical architecture (v1)
│   ├── DEPLOYMENT.md                # Deployment guide
│   ├── MONITORING.md                # Monitoring & observability
│   └── DEVELOPMENT.md               # Development setup guide
│
├── infra/                           # Infrastructure as code
│   └── docker/                      # Docker Compose configuration
│
├── .github/                         # GitHub Actions workflows
│   ├── ci.yml                       # Continuous integration
│   └── deploy.yml                   # Deployment pipeline
│
├── package.json                     # Root workspace config
├── pnpm-workspace.yaml              # pnpm workspace definition
├── tsconfig.json                    # Base TypeScript configuration
├── .env.example                     # Environment variables template
├── Dockerfile                       # Multi-stage build
└── docker-compose.yml               # Local development stack
```

---

## Git Branch Strategy

| Branch | Purpose | Deployment |
|--------|---------|------------|
| `main` | Production code | Auto-deploys to production |
| `develop` | Integration branch | Deploys to staging |
| `feature/*` | New features | No automatic deployment |
| `fix/*` | Bug fixes | No automatic deployment |

---

## Security & Compliance Considerations

### Data Privacy (GDPR by Design)
- **No PII in database** — Agent profiles store only business info (name, license #, bio), not personal data
- **Email routing** — Leads sent from agent's own email account; no PII passes through our servers
- **Minimal data collection** — Only store what's needed for the product to function

### General Security
- Rate limiting on all API endpoints
- CSRF protection on forms
- Input sanitization with Zod schemas
- Regular security audits and dependency updates

---

## Compliance Requirements

| Requirement | Scope | Implementation |
|-------------|-------|----------------|
| Equal Housing Opportunity | All pages | Footer component |
| Cookie consent (GDPR) | EU visitors | CookieConsent component |
| CMA disclaimer | CMA reports | Footer on every page + PDF footer |
| License number display | Agent profiles | Config-driven from agent profile |

---

## Open Questions Resolved

| Question | Decision |
|----------|----------|
| Database choice? | PostgreSQL (free tier), no PII stored, GDPR-compliant by design |
| Email provider? | Agent/brokerage personal accounts for more personal lead delivery |
| Auth provider? | Flexible — must support multi-agent-sites easily |
| Billing? | Stripe, but lowest priority — do last |
| Report formats? | Both PDF and HTML supported |

---

*Last updated: 2026-07-01*
*Author: Product Team*