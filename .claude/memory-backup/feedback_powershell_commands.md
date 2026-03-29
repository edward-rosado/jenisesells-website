---
name: powershell-single-line-commands
description: Always give Eddie single-line PowerShell commands — multi-line with backticks breaks when pasted. No newlines in commands.
type: feedback
---

## PowerShell Command Formatting

- **NEVER give multi-line commands** — Eddie pastes into PowerShell and newlines cause parse errors
- **Always give single-line commands** that can be copy-pasted directly
- Use semicolons to chain commands if needed: `cmd1; cmd2`
- For long az/git commands, keep them on ONE line even if it's wide
- This applies to ALL terminal commands, not just PowerShell
