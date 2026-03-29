---
name: Windows Git Path Workaround
description: git show/archive on Windows converts colon in ref:path syntax — use git archive + tar to temp dir instead
type: feedback
---

# Windows Git Path Issue (2026-03-14)

## Problem
`git show "upstream/main:.claude/skills/cma/SKILL.md"` fails on Windows because Git for Windows converts the colon in `ref:path` syntax to a semicolon. This breaks every time.

## Solution
Don't use `git show ref:path` on Windows. Instead:

```bash
# Extract to temp dir and read from there
mkdir -p /tmp/upstream-review && git archive upstream/main | tar -xC /tmp/upstream-review

# Then read files from /tmp/upstream-review/...
# BUT /tmp/ is a Unix path — use bash `cat` to read, not the Read tool (which needs Windows absolute paths)
```

## Also
- The Read tool requires Windows absolute paths (C:\...), not Unix paths (/tmp/...)
- Use `cat` via Bash tool for files in /tmp/
- Or copy files to a Windows path first before using Read tool
