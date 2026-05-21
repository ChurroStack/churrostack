#!/bin/sh
set -e

export DISPLAY=:99
echo "Starting Xvfb on DISPLAY=$DISPLAY..."
Xvfb :99 -screen 0 1920x1080x24 >/dev/null 2>&1 &

while [ ! -e /tmp/.X11-unix/X99 ]; do
  sleep 0.1
done
echo "Xvfb running on DISPLAY=$DISPLAY"

echo "Starting DBus session"
eval $(dbus-launch --sh-syntax)

echo "Starting OpenBox Desktop Environment"
xsetroot -solid "#3A6EA5" &
openbox-session >/dev/null 2>&1 &

echo "Starting x11vnc server..."
x11vnc \
  -shared \
  -forever \
  -nopw \
  -rfbport 5900 \
  -display :99 \
  -listen 0.0.0.0 \
  -quiet \
  -no6 >/dev/null 2>&1 &

echo "VNC server started on port 5900"
websockify -D --web /usr/share/novnc/ 3003 localhost:5900 &
echo "noVNC viewable at http://localhost:3003"

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