#!/usr/bin/env bash
# Thin wrapper around tools/build-image.sh (image defined in .github/images.json).
#   ./build.sh                    -> local image python-streamlit-x11:0.0.1-local (+ :local), no push
#   ./build.sh <version> --push   -> build linux/amd64 and push to quay.io
exec "$(git rev-parse --show-toplevel)/tools/build-image.sh" python-streamlit-x11 "$@"
