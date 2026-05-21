#!/bin/sh
docker build --platform linux/amd64 -t quay.io/churrostack/churros-tunnel-proxy:latest -f ./ChurrOS.TunnelService/Dockerfile .
docker push quay.io/churrostack/churros-tunnel-proxy:latest