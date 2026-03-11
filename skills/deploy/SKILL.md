---
name: deploy-site
description: |
  **Site Deployer**: Deploy an agent's website files to their configured hosting provider.
  - MANDATORY TRIGGERS: deploy, push to github, update website, publish site, go live, push changes, update site, publish changes, make it live, push my site, update my site, deploy site
---

# Deploy Agent Site

This skill deploys an agent's website files to their configured hosting provider. All agent-specific values (domain, provider, repo, site files) are read from `config/agents/{agent-id}.json`.

## Prerequisites

- Agent config exists at `config/agents/{agent-id}.json`
- Agent config contains a valid `hosting` block (see Configuration below)
- Site source files exist in the agent's configured workspace or site directory
- Required CLI tools are available for the target provider (e.g., `gh` for GitHub Pages)

## Configuration

The agent's config file must include a `hosting` section:

```jsonc
{
  "identity": {
    "name": "Agent Name",
    "website": "example.com"
  },
  "hosting": {
    "provider": "github-pages",       // Hosting provider key
    "repo": "org/repo-name",          // Source repository
    "branch": "main",                 // Branch to deploy from
    "site_dir": "sites/{agent-id}",   // Path to site source files
    "domain": "{agent.identity.website}" // Custom domain (from identity block)
  }
}
```

## Supported Providers

| Provider | Status | Notes |
|----------|--------|-------|
| GitHub Pages | Supported | Free for public repos; custom domain via CNAME |
| Netlify | TBD | Pending provider analysis |
| Vercel | TBD | Pending provider analysis |
| Cloudflare Pages | TBD | Pending provider analysis |
| AWS S3 + CloudFront | TBD | Pending provider analysis |

Provider selection and comparison analysis is tracked separately. New providers are added by implementing a deploy adapter and registering it in this skill.

## Provider Configuration

Each provider requires different configuration values in the agent's `hosting` block:

### GitHub Pages

```jsonc
{
  "hosting": {
    "provider": "github-pages",
    "repo": "org/repo-name",
    "branch": "main",           // or "gh-pages"
    "domain": "example.com"     // Sets CNAME file automatically
  }
}
```

- Requires: `gh` CLI authenticated, write access to the repository
- Custom domain: A `CNAME` file is placed in the deploy branch root
- DNS: Agent must configure an A record or CNAME pointing to GitHub Pages

### Future Providers

Each new provider adapter must define:
1. **Required config keys** — What the `hosting` block needs (API keys, project IDs, etc.)
2. **CLI or API dependency** — What tooling must be installed or available
3. **Deploy mechanism** — Git push, CLI upload, API call, etc.
4. **Domain setup** — How custom domains are configured on the provider side

## Site Files

Site files are located in the path specified by `hosting.site_dir` in the agent config. Typical contents:

| File Pattern | Purpose |
|--------------|---------|
| `index.html` | Main website entry point |
| `*.html` | Additional pages (landing pages, town pages, etc.) |
| `assets/` | Images, stylesheets, scripts |
| `CNAME` | Custom domain file (auto-generated for GitHub Pages) |

The exact set of files varies per agent. Always read the agent's site directory to determine what will be deployed.

## Deployment Process

### Step 1: Load agent configuration

Read `config/agents/{agent-id}.json` and extract the `hosting` block. Validate that all required fields for the target provider are present.

### Step 2: Identify changed files

Compare the agent's local site files (`hosting.site_dir`) against what is currently deployed. Show the user a summary of additions, modifications, and deletions before proceeding.

### Step 3: Confirm with user

Present the list of changes and the target environment (provider, repo, domain). Wait for explicit confirmation before deploying.

### Step 4: Deploy via provider

Route to the appropriate deploy method based on `hosting.provider`:

**GitHub Pages:**
1. Clone or pull the deploy repository (`hosting.repo`, `hosting.branch`)
2. Copy site files from `hosting.site_dir` into the repo working tree
3. Ensure `CNAME` file contains `hosting.domain`
4. Commit with a descriptive message including timestamp
5. Push to the deploy branch

**Fallback — Guided manual deploy:**
If CLI tools are unavailable, walk the user through the provider's web UI to upload files manually.

### Step 5: Verify deployment

1. Wait approximately 30-60 seconds for the provider to process the deploy
2. Check that `https://{agent.identity.website}` responds with HTTP 200
3. Take a screenshot of the live site if screenshot tooling is available
4. Report success or failure to the user

## Rollback

Previous versions can be restored from git commit history:

```bash
# List recent deploys
git log --oneline -10

# Revert to a previous deploy
git revert <commit-hash>
git push
```

For non-git providers, rollback procedures will be documented per provider adapter.

## Legal & Compliance Requirements

Every deployed agent site **must** meet the following before going live. These are non-negotiable.

### Broker Name & License Number (State Law)
- The agent's **brokerage name** and **license number** must appear on every page, typically in the footer.
- Many states (NJ, CA, TX) have specific rules about font size and placement.
- These values come from `{agent.identity.brokerage}` and `{agent.identity.license_id}` — they must not be optional or hidden behind a conditional.
- If either field is missing from the agent config, **halt deployment** and require the agent to provide them.

### Equal Housing Opportunity (Fair Housing Act)
- Every deployed site must display the **Equal Housing Opportunity** logo or text.
- This is a federal requirement for all real estate advertising under the Fair Housing Act.
- The logo and notice are rendered in the footer component — do not remove or make conditional.

### WCAG 2.1 AA Accessibility (ADA Title III)
- All deployed agent sites must meet **WCAG 2.1 Level AA** accessibility standards.
- Real estate websites have been specifically targeted in ADA Title III lawsuits.
- Since we deploy sites on agents' behalf, both the agent and the platform carry liability.
- Key requirements:
  - Sufficient color contrast ratios (4.5:1 for normal text, 3:1 for large text)
  - All images must have `alt` text
  - All interactive elements must be keyboard-accessible
  - Semantic HTML (`nav`, `main`, `footer`, headings in order)
  - Form inputs must have associated `<label>` elements
  - `lang` attribute on `<html>` element
- Run an accessibility audit (axe, Lighthouse) before marking deployment complete.

## Important Notes

- Never deploy without reading and validating the agent config first
- Always verify file contents match intended changes before deploying
- Never hardcode agent names, domains, repos, or provider project IDs in this skill
- All agent-specific values come from `config/agents/{agent-id}.json`
- If the agent config is missing or incomplete, halt and inform the user
