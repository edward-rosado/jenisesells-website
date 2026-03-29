---
name: feedback_gh_log_piping
description: "gh run view --log-failed output cannot be piped to grep/head on Windows — use without piping"
type: feedback
---

`gh run view --log-failed` output cannot be piped to `grep` or `head` on Windows — the pipe fails with "command not found" errors. Use the command without piping and read the full output instead.

**Why:** The `gh` CLI on Windows spawns a subprocess that loses access to bash builtins when piped. This has wasted time multiple times trying to filter CI logs.

**How to apply:** When checking CI failures, run `gh run view <id> --log-failed` without any pipe operators. Read the full output directly.
