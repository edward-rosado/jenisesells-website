---
name: docker-config-files-must-be-in-image
description: Agent config files at repo root must be copied into Docker build context — they're outside apps/api/ and won't be included automatically
type: feedback
---

Config files at `config/accounts/` live at the repo root but the Docker build context is `apps/api/`. They are NOT automatically included in the image.

**Why:** The API couldn't find any agents in production (404 on all lead submissions) because `AccountConfigService` reads from filesystem but the config directory didn't exist in the container. Multiple deploy cycles wasted debugging this.

**How to apply:**
- CI workflow must `cp -r config/accounts apps/api/config-accounts` before `docker build`
- Dockerfile must `COPY config-accounts/ /app/config/accounts/`
- Program.cs checks Docker path first (`/app/config/accounts`), falls back to relative path for local dev
- When adding new config directories that the API needs, always check if they're inside the Docker build context
