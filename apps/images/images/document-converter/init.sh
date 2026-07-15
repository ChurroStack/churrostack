#!/bin/sh
set -e

mkdir -p "${WORK_DIR:-/app/home/jobs}"

echo "Starting document-converter on :8000 (base_path='${BASE_PATH:-}')..."
exec uvicorn app.main:app --host 0.0.0.0 --port 8000
