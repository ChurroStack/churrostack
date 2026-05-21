#!/bin/sh
docker build --platform linux/amd64 -t quay.io/churrostack/churrun-tunnel:latest .
docker push quay.io/churrostack/churrun-tunnel:latest