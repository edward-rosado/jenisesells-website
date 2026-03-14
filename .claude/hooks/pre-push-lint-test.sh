#!/usr/bin/env bash
# pre-push-lint-test.sh
# Claude Code PreToolUse hook — intercepts git push commands and runs
# lint + tests on all apps with changes before allowing the push.
#
# Exit codes:
#   0 = allow (not a git push, or lint+tests pass)
#   2 = block (lint or tests failed — stderr message goes back to Claude)

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

FAILED=""

# Run lint + tests for each app that has a package.json
for app_dir in "$PROJECT_DIR"/apps/*/; do
  app_name=$(basename "$app_dir")

  # Skip apps without package.json
  if [ ! -f "$app_dir/package.json" ]; then
    continue
  fi

  # Check if this app has changes staged or committed since the remote tracking branch
  CURRENT_BRANCH=$(git -C "$PROJECT_DIR" rev-parse --abbrev-ref HEAD 2>/dev/null)
  REMOTE_REF=$(git -C "$PROJECT_DIR" rev-parse --verify "origin/$CURRENT_BRANCH" 2>/dev/null || echo "")

  if [ -n "$REMOTE_REF" ]; then
    CHANGED=$(git -C "$PROJECT_DIR" diff --name-only "$REMOTE_REF"..HEAD -- "apps/$app_name/" 2>/dev/null || echo "")
  else
    # New branch — consider all files as changed
    CHANGED="new-branch"
  fi

  # Skip if no changes in this app
  if [ -z "$CHANGED" ]; then
    continue
  fi

  # Check if lint script exists
  HAS_LINT=$(node -e "const p=require('$app_dir/package.json'); process.exit(p.scripts && p.scripts.lint ? 0 : 1)" 2>/dev/null && echo "yes" || echo "no")

  if [ "$HAS_LINT" = "yes" ]; then
    if ! (cd "$app_dir" && npx eslint . 2>&1); then
      FAILED="${FAILED}  - apps/$app_name: lint failed\n"
    fi
  fi

  # Check if test script exists
  HAS_TEST=$(node -e "const p=require('$app_dir/package.json'); process.exit(p.scripts && p.scripts.test ? 0 : 1)" 2>/dev/null && echo "yes" || echo "no")

  if [ "$HAS_TEST" = "yes" ]; then
    if ! (cd "$app_dir" && npx vitest run 2>&1); then
      FAILED="${FAILED}  - apps/$app_name: tests failed\n"
    fi
  fi
done

if [ -n "$FAILED" ]; then
  echo "Pre-push checks failed:" >&2
  echo -e "$FAILED" >&2
  echo "Fix the issues above before pushing." >&2
  exit 2
fi

exit 0
