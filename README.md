# Real Estate Star

A SaaS platform that automates real estate agent workflows — from lead response and market analysis to contract drafting and website deployment.

## Why

Small independent agents spend hours on repetitive tasks: responding to leads, pulling comps, drafting contracts, updating their website. Real Estate Star automates all of it with AI-powered skills that read from a simple JSON config per agent.

## Quick Start

```bash
git clone https://github.com/edward-rosado/Real-Estate-Star.git
cd Real-Estate-Star
bash setup.sh
```

The setup script checks prerequisites (Node.js 20+, .NET SDK, git), installs dependencies, restores NuGet packages, and validates agent configurations.

See [docs/onboarding.md](docs/onboarding.md) for the full contributor guide.

## Repo Structure

```
apps/
  agent-site/          Next.js 16 — white-label agent websites (built)
  api/                 .NET — backend API (scaffolded)
  portal/              Next.js 16 — Real Estate Star admin portal (planned)
packages/
  shared-types/        TypeScript types shared across apps
  ui/                  Shared UI component library
config/
  agent.schema.json    JSON Schema for agent profiles
  agents/              Per-tenant agent configurations
prototype/             Original jenisesellsnj.com static site
infra/                 Infrastructure and hosting config
docs/                  Design docs, onboarding, plans
```

## Multi-Tenant Architecture

Every agent (tenant) has a JSON config file at `config/agents/{agent-id}.json`. All apps and skills read from this config — no hardcoded agent data anywhere.

```json
{
  "id": "jenise-buckalew",
  "identity": { "name": "Jenise Buckalew", "email": "...", "phone": "..." },
  "location": { "state": "NJ", "service_areas": ["Middlesex County", "..."] },
  "branding": { "primary_color": "#1B5E20", "accent_color": "#C8A951" },
  "integrations": { "email_provider": "gmail", "form_handler": "formspree" },
  "compliance": { "state_form": "NJ-REALTORS-118" }
}
```

Adding a new agent = creating a new JSON file. No code changes needed.

## Agent Site

The white-label agent website engine (`apps/agent-site/`) renders a fully branded site per agent from their JSON config. Features:

- **Template engine** — pluggable templates (currently: `emerald-classic`)
- **Dynamic branding** — colors, fonts, and content from agent config
- **SEO** — `generateMetadata`, `robots.ts`, `sitemap.ts`, JSON-LD structured data
- **Security** — nonce-based CSP via middleware, security headers
- **Analytics** — per-agent Google Analytics, GTM, Google Ads, Meta Pixel
- **Observability** — Sentry error tracking
- **CMA form** — lead capture form with Formspree integration
- **ISR** — incremental static regeneration with 60-second revalidation

### Running the agent site

```bash
cd apps/agent-site
npm install
npm run dev          # http://localhost:3000?agentId=jenise-buckalew
npm test             # 198 tests, 100% coverage enforced
npm run build        # production build
```

## API

The backend API (`apps/api/`) is a .NET Minimal API with:

- Serilog structured logging
- Swagger/OpenAPI documentation (dev only)
- CORS policy for agent site subdomains
- Health check endpoint at `/healthz`
- Integration tests via `WebApplicationFactory`

```bash
cd apps/api
dotnet restore src/RealEstateStar.Api/RealEstateStar.Api.csproj
dotnet run --project src/RealEstateStar.Api
dotnet test
```

## Tech Stack

| Component | Technology | Status |
|-----------|-----------|--------|
| Agent Sites | Next.js 16, React 19, Tailwind CSS 4 | Built |
| API | .NET Minimal API, Serilog, QuestPDF | Scaffolded |
| Portal | Next.js 16 | Planned |
| Config | JSON + JSON Schema | Built |
| Testing | Vitest + v8 coverage (frontend), xUnit (API) | Built |
| AI Tooling | Claude Code with custom skills | Active |
| PM | GitHub Issues + Projects | Active |

## Adding a New Agent

1. Copy `config/agents/jenise-buckalew.json` as a template
2. Update all fields for the new agent
3. Create a content file at `config/agents/{agent-id}.content.json` with hero text, services, testimonials, etc.
4. Visit `http://localhost:3000?agentId={agent-id}` to preview

## License

Private — not open source.
