#!/usr/bin/env bash
#
# build-image.sh — build (and optionally push) a ChurroStack container image.
#
#   tools/build-image.sh <image-name> [version] [--push]
#
# <image-name>  An image declared in .github/images.json (e.g. churros-api).
# [version]     Image version. Defaults to 0.0.1-local.
# [--push]      Push to the registry instead of loading locally.
#
# Local default (no --push): builds for the host's native architecture and
# loads the image into the local Docker daemon, tagged <name>:<version> and
# <name>:local. It never touches <name>:latest, so a dev build cannot shadow a
# released image. Nothing is pushed.
#
# With --push: forces --platform linux/amd64 (production parity) and pushes
# <name>:<version> and <name>:latest. Requires `docker login quay.io` first.
# CI uses this form with the release-please version.
#
# This script and the GitHub Actions workflows share .github/images.json as the
# single source of truth — there is no per-image build logic anywhere else.
set -eo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="$REPO_ROOT/.github/images.json"

usage() {
  echo "usage: tools/build-image.sh <image-name> [version] [--push]" >&2
  exit 2
}

IMAGE=""
VERSION="0.0.1-local"
PUSH="false"

for arg in "$@"; do
  case "$arg" in
    --push) PUSH="true" ;;
    -h|--help) usage ;;
    -*) echo "unknown option: $arg" >&2; usage ;;
    *)
      if [ -z "$IMAGE" ]; then IMAGE="$arg"; else VERSION="$arg"; fi
      ;;
  esac
done

[ -n "$IMAGE" ] || usage
[ -f "$MANIFEST" ] || { echo "manifest not found: $MANIFEST" >&2; exit 1; }

# Resolve the image entry from the manifest. node is always available in this
# repo (it is an Nx + pnpm workspace), so no jq dependency is needed.
ENTRY="$(node -e '
  const fs = require("fs");
  const m = JSON.parse(fs.readFileSync(process.argv[1], "utf8"));
  const img = (m.images || []).find(i => i.name === process.argv[2]);
  if (!img) {
    console.error("unknown image: " + process.argv[2]);
    console.error("known images: " + (m.images || []).map(i => i.name).join(", "));
    process.exit(1);
  }
  process.stdout.write([m.registry, img.context, img.dockerfile, img.versionArg ? "1" : "0"].join("\t"));
' "$MANIFEST" "$IMAGE")"

IFS=$'\t' read -r REGISTRY CONTEXT DOCKERFILE VERSION_ARG <<< "$ENTRY"

REF="$REGISTRY/$IMAGE"

BUILD_CMD=(docker buildx build)
BUILD_CMD+=(-t "$REF:$VERSION")

if [ "$PUSH" = "true" ]; then
  BUILD_CMD+=(-t "$REF:latest")
  BUILD_CMD+=(--platform linux/amd64)
  BUILD_CMD+=(--push)
else
  # Local build: extra :local convenience tag, native arch, load into daemon.
  BUILD_CMD+=(-t "$REF:local")
  BUILD_CMD+=(--load)
fi

if [ "$VERSION_ARG" = "1" ]; then
  BUILD_CMD+=(--build-arg "VERSION=$VERSION")
fi

BUILD_CMD+=(-f "$REPO_ROOT/$DOCKERFILE")
BUILD_CMD+=("$REPO_ROOT/$CONTEXT")

echo "==> $IMAGE  version=$VERSION  push=$PUSH"
echo "    ${BUILD_CMD[*]}"
exec "${BUILD_CMD[@]}"
