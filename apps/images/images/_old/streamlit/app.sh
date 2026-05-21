#!/bin/sh
set -e

ENTRY_FILE="${ENTRY_FILE:-streamlit_app.py}"

if [ -f /app/home/code/requirements.txt ]; then
    echo "Installing dependencies from requirements.txt"
    pip install -rU /app/home/code/requirements.txt
else
    echo "No requirements.txt found, skipping dependency install"
fi

if [ ! -f /app/home/code/$ENTRY_FILE ]; then
    echo "Error: Entry file '$ENTRY_FILE' not found in /app/home/code/"
    exit 1
fi

echo "Starting Streamlit app..."
python -m streamlit run /app/home/code/$ENTRY_FILE --browser.gatherUsageStats false --server.port 8501
