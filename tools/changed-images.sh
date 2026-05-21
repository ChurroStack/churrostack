#!/usr/bin/env bash
#
# changed-images.sh — print the images whose files changed between two git refs.
#
#   tools/changed-images.sh <base-ref> <head-ref>
#
# Emits a single-line JSON array of image entries (from .github/images.json)
# whose 'watch' paths changed between <base-ref> and <head-ref>. The output is
# consumed directly as a GitHub Actions build matrix.
#
# Comparison uses the merge-base of the two refs, so it is correct both for a
# pull request (base branch vs PR head) and for a release (previous release tag
# vs release commit).
#
# If <base-ref> is empty or cannot be resolved (the first release / bootstrap),
# ALL images are emitted.
set -eo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="$REPO_ROOT/.github/images.json"

BASE="${1:-}"
HEAD="${2:-HEAD}"

CHANGED=""
if [ -n "$BASE" ] && git -C "$REPO_ROOT" rev-parse --verify --quiet "${BASE}^{commit}" >/dev/null 2>&1; then
  MERGE_BASE="$(git -C "$REPO_ROOT" merge-base "$BASE" "$HEAD" 2>/dev/null || echo "$BASE")"
  CHANGED="$(git -C "$REPO_ROOT" diff --name-only "$MERGE_BASE" "$HEAD")"
else
  BASE=""  # bootstrap: no usable base, build everything
fi

node -e '
  const fs = require("fs");
  const m = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
  const base = process.argv[2];
  const changed = process.argv[3] ? process.argv[3].split("\n").filter(Boolean) : [];
  const images = m.images || [];
  const matched = (img) => {
    if (!base) return true; // bootstrap: build all
    const watch = [].concat(img.watch || []);
    return changed.some((f) =>
      watch.some((p) => f === p || f.startsWith(p.replace(/\/*$/, "") + "/"))
    );
  };
  process.stdout.write(JSON.stringify(images.filter(matched)));
' "$MANIFEST" "$BASE" "$CHANGED"
echo
