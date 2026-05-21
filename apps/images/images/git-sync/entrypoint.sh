#!/usr/bin/env sh
set -eu

: "${GIT_REPOSITORY_URL:?GIT_REPOSITORY_URL is required}"

GIT_REPOSITORY_BRANCH="${GIT_REPOSITORY_BRANCH:-main}"
SYNC_DIR="${SYNC_DIR:-/app/home/code}"
SYNC_PERIOD="${SYNC_PERIOD:-60}"

export GIT_TERMINAL_PROMPT=0

# Build repo URL
if [ -n "${GIT_REPOSITORY_USERNAME:-}" ] && [ -n "${GIT_REPOSITORY_PASSWORD:-}" ]; then
  URL="${GIT_REPOSITORY_URL#https://}"
  URL="${URL#http://}"
  REPO_URL="https://${GIT_REPOSITORY_USERNAME}:${GIT_REPOSITORY_PASSWORD}@${URL}"
else
  REPO_URL="${GIT_REPOSITORY_URL}"
fi

echo "Repo URL: ${GIT_REPOSITORY_URL}"
echo "Branch: ${GIT_REPOSITORY_BRANCH}"
echo "Sync dir: ${SYNC_DIR}"
echo "Period: ${SYNC_PERIOD}s"

# Initial clone if needed
if [ ! -d "${SYNC_DIR}/.git" ]; then
  echo "Cloning repository..."
  git clone \
    --branch "${GIT_REPOSITORY_BRANCH}" \
    --single-branch \
    "${REPO_URL}" \
    "${SYNC_DIR}"
fi

cd "${SYNC_DIR}"

while true; do
  git fetch --depth=1 origin "${GIT_REPOSITORY_BRANCH}"

  if ! git diff --quiet HEAD "origin/${GIT_REPOSITORY_BRANCH}"; then
    echo "$(date) - Changes detected, syncing"
    git reset --hard "origin/${GIT_REPOSITORY_BRANCH}"
    git clean -fd
  fi

  sleep "${SYNC_PERIOD}"
done