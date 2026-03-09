#!/usr/bin/env bash
# pre-push-readme.sh
# Claude Code PreToolUse hook — intercepts git push commands and ensures
# README.md is up to date before allowing the push.
#
# Exit codes:
#   0 = allow (not a git push, or README is current)
#   2 = block (README needs updating — stderr message goes back to Claude)

set -euo pipefail

INPUT=$(cat)
COMMAND=$(echo "$INPUT" | jq -r '.tool_input.command // empty' 2>/dev/null)

# Only intercept git push commands
if ! echo "$COMMAND" | grep -qE '^\s*git\s+(push|(-C\s+\S+\s+)?push)'; then
  exit 0
fi

PROJECT_DIR=$(echo "$INPUT" | jq -r '.cwd // empty' 2>/dev/null)
if [ -z "$PROJECT_DIR" ]; then
  PROJECT_DIR="$CLAUDE_PROJECT_DIR"
fi

README="$PROJECT_DIR/README.md"

# Block if README doesn't exist at all
if [ ! -f "$README" ]; then
  echo "README.md is missing. Create or update it before pushing." >&2
  exit 2
fi

# Check if README was modified more recently than the last commit that touched it
README_LAST_COMMIT=$(git -C "$PROJECT_DIR" log -1 --format="%H" -- README.md 2>/dev/null || echo "")

if [ -z "$README_LAST_COMMIT" ]; then
  echo "README.md exists but has never been committed. Stage and commit it before pushing." >&2
  exit 2
fi

# Check if there are structural changes since README was last updated
# Structural = new/deleted/renamed files in key directories
README_COMMIT_DATE=$(git -C "$PROJECT_DIR" log -1 --format="%aI" -- README.md 2>/dev/null)

STRUCTURAL_CHANGES=$(git -C "$PROJECT_DIR" log --since="$README_COMMIT_DATE" \
  --diff-filter=ADR --name-only --pretty=format: -- \
  'apps/' 'packages/' 'skills/' 'config/' 'infra/' 'docs/' \
  2>/dev/null | grep -v '^$' | head -20)

if [ -n "$STRUCTURAL_CHANGES" ]; then
  echo "README.md may be outdated. The following structural changes happened since it was last updated:" >&2
  echo "$STRUCTURAL_CHANGES" >&2
  echo "" >&2
  echo "Please review README.md and update it if needed, then commit before pushing." >&2
  exit 2
fi

# README exists, is committed, and no structural changes detected
exit 0
