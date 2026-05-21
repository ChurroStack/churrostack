#!/bin/sh
set -eu

echo "Starting ChurrOS SSH Tunnel Container..."

# Persist selected runtime env vars for AuthorizedKeysCommand
echo "PGHOST=${PGHOST:-}" > /run/authorized_keys_env
echo "PGPORT=${PGPORT:-}" >> /run/authorized_keys_env
echo "PGUSER=${PGUSER:-}" >> /run/authorized_keys_env
echo "PGDATABASE=${PGDATABASE:-}" >> /run/authorized_keys_env

if [ ! -f /etc/ssh/ssh_host_ed25519_key ]; then
    echo "Generating SSH host keys..."
    ssh-keygen -A
    cp /sshd_config /etc/ssh/sshd_config
fi

touch ~/.pgpass
chmod 600 ~/.pgpass
echo "${PGHOST:-localhost}:${PGPORT:-5432}:${PGDATABASE:-ChurrOS}:${PGUSER:-tunnel}:${PGPASSWORD:-}" > ~/.pgpass

echo "Starting sshd..."

# Hand off to sshd (via tini)
exec "$@"