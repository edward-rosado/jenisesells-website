---
name: blob-index-tags-for-lead-queries
description: Azure Blob Index Tags can replace full-document rewrites for status updates and enable efficient lead queries without scanning all blobs
type: reference
---

# Azure Blob Index Tags for Lead Storage Optimization

## Problem Observed (2026-03-25)

Lead Profile.md is rewritten 4+ times per pipeline run because `UpdateStatusAsync` reads the full markdown, updates one YAML frontmatter field, and writes the entire document back through FanOutStorageProvider. Each write fans out to 3 tiers (Drive no-ops, Blob succeeds = noisy `[BLOB-001]` logs).

## Proposed Solution: Blob Index Tags

Azure Blob Index Tags are indexed key-value pairs on each blob, queryable across the container via `FindBlobsByTags()`.

**For status updates:** Set a `status` tag instead of rewriting the full markdown. Only rewrite the document when actual content changes (enrichment, not status transitions).

**For queries:** Replace `ListByStatusAsync` / `GetByEmailAsync` (currently reads every blob and parses YAML frontmatter) with tag queries:
```csharp
containerClient.FindBlobsByTags("\"agentId\" = 'jenise-buckalew' AND \"status\" = 'Notified'");
```

**Suggested tags per lead blob:**
- `agentId` — for per-agent filtering
- `status` — Received/Enriched/EmailDrafted/Notified/Complete
- `email` — for dedup lookups (hashed for PII safety)
- `leadType` — Buyer/Seller
- `leadId` — for direct lookup

**Constraints:** Max 10 tags per blob, 256 char key, 1024 char value. Free — included in storage cost.

## Decision Needed

1. **Quick fix:** Add tags alongside current writes — status updates still rewrite the doc but tags enable fast queries
2. **Full optimization:** Status updates ONLY set the tag (no doc rewrite). Requires separating "content writes" from "metadata writes" in the storage interface
3. **Full refactor:** Replace YAML frontmatter-based storage with a tag-first model — LeadFileStore queries via tags, only reads blob content when full lead data is needed

Option 1 is additive and backward-compatible. Options 2-3 are bigger refactors that change the storage abstraction.
