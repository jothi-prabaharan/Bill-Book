# The four browser apps — web, portal, admin, docs — built from source and
# served by one nginx. Build context is `frontend`; nginx.conf arrives through
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

# The four the browser serves. desktop is an Electron shell and has no place
# behind nginx. NX_DAEMON off because a build container has no use for a
# background process that outlives the command.
ENV NX_DAEMON=false
RUN npx nx run-many -t build --projects=web,portal,admin,docs

# ---------------------------------------------------------------------------
# Serve
# ---------------------------------------------------------------------------
FROM nginx:1.27-alpine

COPY --from=build /src/dist/apps/web/browser    /usr/share/nginx/web
COPY --from=build /src/dist/apps/portal/browser /usr/share/nginx/portal
COPY --from=build /src/dist/apps/admin/browser  /usr/share/nginx/admin
COPY --from=build /src/dist/apps/docs/browser   /usr/share/nginx/docs

# Replaces the image's default site rather than sitting beside it.
COPY --from=config nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=config app.conf   /etc/nginx/app.conf

EXPOSE 8081 8082 8083 8084
