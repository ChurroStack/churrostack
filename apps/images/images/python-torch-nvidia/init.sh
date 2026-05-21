#!/bin/sh
set -e

echo "Starting ttyd terminal server..."
ttyd -p 3001 -w /app/home -b ${BASE_PATH}/terminal -W bash &

mkdir -p /app/home/code /app/home/python
cd /app/home/code
python -m venv /app/home/python/venv
export PATH=/app/home/python/venv/bin:$PATH

python -m pip install altair pandas streamlit numpy fastapi uvicorn pydantic python-dotenv requests

if [ -f /app/home/code/requirements.txt ]; then
  echo "Installing dependencies from requirements.txt"
  pip install --no-cache-dir -r /app/home/code/requirements.txt
else
  echo "No requirements.txt found, skipping dependency install"
fi

# Try restart
echo "Starting python with args: $@"
until python $@; do
  echo "Failed. Retry in 5 seconds..."
  sleep 5
  if [ -f /app/home/code/requirements.txt ]; then
    echo "Installing dependencies from requirements.txt"
    pip install --no-cache-dir -r /app/home/code/requirements.txt
  else
    echo "No requirements.txt found, skipping dependency install"
  fi
  echo "Starting python with args: $@"
done