#!/bin/sh
docker build --platform linux/amd64 -t quay.io/churrostack/churros-tunnel:latest .
docker push quay.io/churrostack/churros-tunnel:latest