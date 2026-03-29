---
name: gws-cli-not-in-docker
description: The gws CLI tool is not installed in the API Docker container — email/Drive operations via GwsCliRunner fail in production
type: feedback
---

`GwsCliRunner` shells out to `gws` binary which doesn't exist in the Docker container. All operations (Gmail send, Drive create, Sheets append) fail in production.

**Why:** The `gws` CLI was used during local development but never added to the Dockerfile. `MultiChannelLeadNotifier.SendEmailAsync` fails immediately because `Process.Start("gws")` throws "file not found".

**How to apply:**
- Don't assume CLI tools available locally are in Docker — check the Dockerfile
- For email: either install `gws` in Docker + configure OAuth, or switch to Gmail API client (HTTP-based)
- The `RealEstateStar.Clients.Gmail` project exists but is empty (placeholder)
- For now, notification failures are handled by retry + dead letter, but email delivery is broken in prod
- Eddie wants to keep the CLI approach — the fix is installing `gws` in Docker and configuring credentials
