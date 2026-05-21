FROM node:20-alpine AS builder

# Set working directory
WORKDIR /app

# Install dependencies first (better caching)
COPY package.json package-lock.json* pnpm-lock.yaml* yarn.lock* ./

# Install deps (auto-detect package manager)
RUN \
  if [ -f package-lock.json ]; then npm ci; \
  elif [ -f pnpm-lock.yaml ]; then corepack enable && pnpm install --frozen-lockfile; \
  elif [ -f yarn.lock ]; then yarn install --frozen-lockfile; \
  else npm install; fi

# Copy source
COPY . .

# Build the app
RUN npm run build

# ---------- Production stage ----------
FROM nginxinc/nginx-unprivileged:alpine AS production

USER root

# Remove default nginx static files
RUN rm -rf /usr/share/nginx/html/*

# Copy built assets from builder
COPY --chown=101:101 --from=builder /app/dist /usr/share/nginx/html
COPY --chown=101:101 public/env.js /tmp/env.js

# Copy custom nginx config (optional but recommended)
COPY --chown=101:101 nginx.conf /etc/nginx/conf.d/default.conf
COPY --chown=101:101 entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh

USER 101:101

# Expose port
EXPOSE 8000

# Start nginx
ENTRYPOINT ["/entrypoint.sh"]