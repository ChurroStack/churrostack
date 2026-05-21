#!/usr/bin/env bash
set -euo pipefail

: "${SSH_HOST:?Missing SSH_HOST}"
: "${SSH_USER:?Missing SSH_USER}"
: "${REMOTE_PORT:?Missing REMOTE_PORT}"
: "${LOCAL_PORT:?Missing LOCAL_PORT}"

# Optional known_hosts injection
if [[ -n "${SSH_KNOWN_HOSTS:-}" ]]; then
  echo "$SSH_KNOWN_HOSTS" > /home/tunnel/.ssh/known_hosts
fi

if [[ ! -f "/home/tunnel/.ssh/keys/id_ed25519" ]]; then
  echo "ERROR: SSH key not found at /home/tunnel/.ssh/keys/id_ed25519"
  exit 1
fi

exec autossh \
  -M 0 \
  -N \
  -o "ServerAliveInterval=30" \
  -o "ServerAliveCountMax=3" \
  -o "ExitOnForwardFailure=yes" \
  -o "StrictHostKeyChecking=yes" \
  -o "IdentitiesOnly=yes" \
  -i "/home/tunnel/.ssh/keys/id_ed25519" \
  -p "$SSH_PORT" \
  -R "${REMOTE_PORT}:${LOCAL_HOST}:${LOCAL_PORT}" \
  "${SSH_USER}@${SSH_HOST}"