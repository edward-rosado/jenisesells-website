---
name: deploy-website
description: |
  **Website Deployer**: Deploy updated website files from Jenise's workspace folder to her GitHub repository (jenisesellsnj-hash/jenisesells-website), which auto-deploys to jenisesellsnj.com via Netlify.
  - MANDATORY TRIGGERS: deploy, push to github, update website, publish site, go live, push changes, deploy to netlify, update jenisesells, publish changes, make it live, push my site, update my site
  - Also trigger when Jenise says things like "push this to my website", "make these changes live", "update the live site", "deploy the new version", or any casual mention of getting website changes online
  - Even short phrases like "deploy it", "push it live", or "update the site" should trigger this skill
---

# Deploy Website to GitHub + Netlify

This skill deploys Jenise's website files to her live site at **jenisesellsnj.com**.

## Architecture

- **Source files**: Jenise's workspace folder (the mounted folder in the current session)
- **Repository**: `github.com/jenisesellsnj-hash/jenisesells-website` (main branch)
- **Hosting**: Netlify (project: prismatic-naiad-5466af) with auto-deploy from GitHub
- **Domain**: jenisesellsnj.com

When files are pushed to the GitHub repo's `main` branch, Netlify automatically deploys them to the live site within ~30 seconds.

## Website Files

The site consists of these core files:

| File | Purpose |
|------|---------|
| `index.html` | Main website — hero, services, CMA form, testimonials, about |
| `headshot.jpg` | Jenise's professional headshot |
| `logo.png` | Green Light Realty logo |
| `selling-home-*.html` | Local landing pages for NJ towns (Edison, Hazlet, Sayreville, East Brunswick, Toms River) |

## Deployment Process

### Step 1: Identify changed files

Compare the workspace files against what's currently on GitHub. If you have browser access (Claude in Chrome), you can check the repo directly. Otherwise, ask Jenise which files were updated, or check modification timestamps.

### Step 2: Deploy via the best available method

**Method A — GitHub CLI (preferred, if `gh` is available):**

```bash
# Clone, copy updated files, commit, and push
gh repo clone jenisesellsnj-hash/jenisesells-website /tmp/website-deploy
cp <updated-files> /tmp/website-deploy/
cd /tmp/website-deploy
git add -A
git commit -m "Update website files"
git push origin main
```

**Method B — Browser-based (if Claude in Chrome is connected):**

1. Navigate to `github.com/jenisesellsnj-hash/jenisesells-website`
2. For each changed file:
   - Click the file name to open it
   - Click the pencil icon (edit)
   - Select all (Cmd+A) and delete the content
   - Paste the new file content
   - Click "Commit changes..." and confirm
3. For new files: Click "Add file" → "Upload files" and drag them in

**Method C — Guided manual deployment (fallback):**

Walk Jenise through these steps:

1. Go to [github.com/jenisesellsnj-hash/jenisesells-website](https://github.com/jenisesellsnj-hash/jenisesells-website)
2. For each file that needs updating:
   - Click the file → click the "..." menu → "Delete file" → Commit
   - Then "Add file" → "Upload files" → drag in the updated version → Commit
3. Netlify auto-deploys within ~30 seconds

### Step 3: Verify deployment

After pushing changes:

1. Wait ~30 seconds for Netlify to build and deploy
2. Check the live site at `jenisesellsnj.com` (hard refresh with Cmd+Shift+R)
3. If browser access is available, take a screenshot to confirm changes are live
4. Optionally check the Netlify dashboard at `app.netlify.com/projects/prismatic-naiad-5466af/deploys` to confirm the deploy succeeded

## Important Notes

- Always verify the updated files contain the intended changes before deploying
- The Netlify deploy is automatic — no manual trigger needed once files hit GitHub
- If something goes wrong, previous versions can be restored from GitHub's commit history
- The Google Maps API key (`AIzaSyA0ZVT8rnAuHnkAK3gdO26hafDSDpVwI7U`) is embedded in index.html for the address autocomplete feature — keep it intact when making changes
