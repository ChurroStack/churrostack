#!/bin/sh
envsubst < /tmp/env.js > /usr/share/nginx/html/env.js

exec nginx -g 'daemon off;'