#!/bin/sh
set -e

mkdir -p /app/home/code
python -m venv /app/home/python/venv
export PATH=/app/home/python/venv/bin:$PATH

exec dumb-init code-server --disable-telemetry --disable-getting-started-override --auth none --bind-addr 0.0.0.0:8080 --user-data-dir /app/home/code "$@"