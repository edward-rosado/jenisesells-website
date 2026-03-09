#!/usr/bin/env bash
# pre-push-readme.sh
# Claude Code PreToolUse hook — intercepts git push commands and ensures
# README.md and setup scripts are up to date before allowing the push.
#
# Exit codes:
#   0 = allow (not a git push, or everything is current)
#   2 = block (README or setup scripts need updating — stderr message goes back to Claude)

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

# --- Setup script checks ---
# Verify setup scripts reference the same skills and directories that actually exist

ISSUES=""

# Check that setup.sh validates every skill directory that exists
for skill_dir in "$PROJECT_DIR"/skills/*/; do
  skill_name=$(basename "$skill_dir")
  if [ -f "$PROJECT_DIR/setup.sh" ] && ! grep -q "$skill_name" "$PROJECT_DIR/setup.sh" 2>/dev/null; then
    ISSUES="${ISSUES}  - setup.sh doesn't reference skill '$skill_name'\n"
  fi
  if [ -f "$PROJECT_DIR/setup.ps1" ] && ! grep -q "$skill_name" "$PROJECT_DIR/setup.ps1" 2>/dev/null; then
    ISSUES="${ISSUES}  - setup.ps1 doesn't reference skill '$skill_name'\n"
  fi
done

# Check that setup scripts don't reference app directories that no longer exist
for app in portal agent-site api; do
  if [ -f "$PROJECT_DIR/setup.sh" ] && grep -q "apps/$app" "$PROJECT_DIR/setup.sh" 2>/dev/null; then
    if [ ! -d "$PROJECT_DIR/apps/$app" ]; then
      ISSUES="${ISSUES}  - setup.sh references apps/$app but directory doesn't exist\n"
    fi
  fi
done

# Check that setup scripts reference every package directory that has a package.json
for pkg_dir in "$PROJECT_DIR"/packages/*/; do
  pkg_name=$(basename "$pkg_dir")
  if [ -f "$pkg_dir/package.json" ] && [ -f "$PROJECT_DIR/setup.sh" ]; then
    if ! grep -q "packages/$pkg_name" "$PROJECT_DIR/setup.sh" 2>/dev/null; then
      ISSUES="${ISSUES}  - setup.sh doesn't install dependencies for packages/$pkg_name\n"
    fi
  fi
done

if [ -n "$ISSUES" ]; then
  echo "Setup scripts may be out of sync with the repo structure:" >&2
  echo -e "$ISSUES" >&2
  echo "Please review setup.sh and setup.ps1, then commit before pushing." >&2
  exit 2
fi

# All checks passed
exit 0
