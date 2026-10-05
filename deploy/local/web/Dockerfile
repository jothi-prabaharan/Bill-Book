# The two browser apps — web and docs — built from source and served by one
# nginx. The client portal and the platform admin pages are routes inside web
# (/portal, /admin) since TK-131 and TK-132, not apps of their own. Build
# context is `frontend`; nginx.conf arrives through
# the `config` context that docker-compose.yml names, so nothing about the local
# deployment has to live inside the frontend tree.

# ---------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------
FROM node:22-bookworm-slim AS build
WORKDIR /src

# Dependencies first, on their own layer: a source-only change reuses them.
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .

# The two the browser serves. desktop is an Electron shell and has no place
# behind nginx. NX_DAEMON off because a build container has no use for a
# background process that outlives the command.
ENV NX_DAEMON=false
RUN npx nx run-many -t build --projects=web,docs

# ---------------------------------------------------------------------------
# Serve
# ---------------------------------------------------------------------------
FROM nginx:1.27-alpine

COPY --from=build /src/dist/apps/web/browser  /usr/share/nginx/web
COPY --from=build /src/dist/apps/docs/browser /usr/share/nginx/docs

# Replaces the image's default site rather than sitting beside it.
COPY --from=config nginx.conf /etc/nginx/conf.d/default.conf
# A template, so the gateway's address can be set when the container starts
# rather than built in. The image's entrypoint writes it out as
# /etc/nginx/app.conf, substituting only GATEWAY_UPSTREAM — nginx's own $host
# and $uri must pass through untouched.
COPY --from=config app.conf   /etc/nginx/templates/app.conf.template
ENV NGINX_ENVSUBST_OUTPUT_DIR=/etc/nginx \
    NGINX_ENVSUBST_FILTER=^GATEWAY_UPSTREAM$ \
    GATEWAY_UPSTREAM=http://gateway:8080

EXPOSE 8081 8084
