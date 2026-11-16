#!/usr/bin/env sh
set -eu

: "${GIT_REPOSITORY_URL:?GIT_REPOSITORY_URL is required}"

GIT_REPOSITORY_BRANCH="${GIT_REPOSITORY_BRANCH:-main}"
SYNC_DIR="${SYNC_DIR:-/app/home/code}"
SYNC_PERIOD="${SYNC_PERIOD:-60}"

export GIT_TERMINAL_PROMPT=0

# Credentials are supplied through a git credential helper that reads the env
# vars each time git asks. They are never embedded in the remote URL, so they
# are not persisted in ${SYNC_DIR}/.git/config (where a stale PAT would keep
# being used after the env vars change) and need no URL-encoding. The helper is
# injected via env-based git config, so nothing is written to disk.
if [ -n "${GIT_REPOSITORY_USERNAME:-}" ] && [ -n "${GIT_REPOSITORY_PASSWORD:-}" ]; then
  export GIT_CONFIG_COUNT=1
  export GIT_CONFIG_KEY_0="credential.helper"
  export GIT_CONFIG_VALUE_0='!f() { test "$1" = get || exit 0; printf "username=%s\npassword=%s\n" "$GIT_REPOSITORY_USERNAME" "$GIT_REPOSITORY_PASSWORD"; }; f'
  AUTH_MODE="credential helper (user=${GIT_REPOSITORY_USERNAME})"
else
  AUTH_MODE="none"
fi

echo "Repo URL: ${GIT_REPOSITORY_URL}"
echo "Branch: ${GIT_REPOSITORY_BRANCH}"
echo "Sync dir: ${SYNC_DIR}"
echo "Period: ${SYNC_PERIOD}s"
echo "Auth: ${AUTH_MODE}"

# Initial clone if needed
if [ ! -d "${SYNC_DIR}/.git" ]; then
  echo "Cloning repository..."
  git clone \
    --branch "${GIT_REPOSITORY_BRANCH}" \
    --single-branch \
    "${GIT_REPOSITORY_URL}" \
    "${SYNC_DIR}"
else
  echo "Using existing clone in ${SYNC_DIR}"
fi

cd "${SYNC_DIR}"

# Always (re)point origin at the plain URL: scrubs credentials that older
# versions of this script embedded in an existing clone, and picks up URL changes.
git remote set-url origin "${GIT_REPOSITORY_URL}"

while true; do
  git fetch --depth=1 origin "${GIT_REPOSITORY_BRANCH}"

  if ! git diff --quiet HEAD "origin/${GIT_REPOSITORY_BRANCH}"; then
    echo "$(date) - Changes detected, syncing"
    git reset --hard "origin/${GIT_REPOSITORY_BRANCH}"
    git clean -fd
  fi

  sleep "${SYNC_PERIOD}"
done