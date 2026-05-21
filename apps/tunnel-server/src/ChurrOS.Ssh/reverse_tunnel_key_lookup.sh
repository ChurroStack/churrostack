#!/bin/sh
# SSH AuthorizedKeysCommand with Postgres lookup
# $1 = key (ignored in this setup)
set -eu
. /run/authorized_keys_env

# Postgres connection settings
PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGUSER="${PGUSER:-tunnel}"
PGDATABASE="${PGDATABASE:-ChurrOS}"

# Execute query safely using psql's variable substitution
result=$(psql -At -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" \
        -c "SELECT replace(encode(ssh_public_key, 'base64'), E'\n', ''), port FROM cs.environment WHERE ssh_public_key = decode('$1', 'base64') LIMIT 1;")

# Check result
if [ -n "$result" ]; then
    ssh_key=$(echo "$result" | cut -d'|' -f1)
    port=$(echo "$result" | cut -d'|' -f2)
    echo "restrict,port-forwarding,permitlisten=\"localhost:$port\",command=\"echo 'Remote shell access has been disabled\nPlease run ssh with the option -N (and optionally -f)'\" ssh-ed25519 $ssh_key"
    exit 0
else
    exit 1
fi