#!/bin/bash
# ─────────────────────────────────────────────────────────
# setup_git.sh — idempotent repo + module-branch setup for North-Star.
# The orchestrator already ran this once during foundation setup. Re-running is safe:
# it (re)creates any missing module branches off the current main.
#   chmod +x setup_git.sh && ./setup_git.sh
# ─────────────────────────────────────────────────────────
set -e

echo "🎮 North-Star repo / branch setup..."

# Init only if this isn't a git repo yet.
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || git init

# Require at least one commit before branching.
if ! git rev-parse HEAD >/dev/null 2>&1; then
  echo "⚠️  No commits yet. Make the initial commit on main first, then re-run."
  exit 1
fi

# Create the 9 module branches off main (skip any that already exist).
for module in \
  "01-core-architecture" \
  "02-character-customization" \
  "03-battle-mechanics" \
  "04-dialogue-quests" \
  "05-world-building" \
  "06-landscape-environment" \
  "07-player-controller" \
  "08-inventory-economy" \
  "09-audio-polish"
do
  branch="feature/module-$module"
  if git show-ref --verify --quiet "refs/heads/$branch"; then
    echo "↺ exists: $branch"
  else
    git branch "$branch" main
    echo "✅ created: $branch"
  fi
done

echo ""
echo "Branch summary:"
git branch
