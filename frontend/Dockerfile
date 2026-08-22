# syntax=docker/dockerfile:1
#
# Web container — nginx serving the static site and proxying /api/* to the API
# container. There is no build step: these are hand-written HTML/CSS/JS files.
#
# Build context is the "frontend" folder:  docker build -t invitation-platform/web:local ./frontend

# ── Staging stage ─────────────────────────────────────────────────────────────
# The container plumbing lives in this folder, so it has to be in the build context —
# which means .dockerignore cannot keep it out of a "COPY .". Strip it here instead, so
# the final image's web root holds only the site (nothing left behind in an earlier layer).
FROM alpine:3.20 AS site
WORKDIR /site
COPY . .
RUN rm -f Dockerfile .dockerignore nginx.conf

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM nginx:1.27-alpine

# Replace the stock default site rather than adding alongside it, so there is
# exactly one server block and no surprise precedence.
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=site /site /usr/share/nginx/html

EXPOSE 80

# wget ships with the alpine image's busybox, so no extra package is needed.
# 127.0.0.1 rather than "localhost": the probe should test the server, not the container's
# name-resolution order, and it must not depend on the IPv6 listener being present.
HEALTHCHECK --interval=15s --timeout=3s --start-period=5s --retries=3 \
  CMD wget -q --spider http://127.0.0.1/healthz || exit 1
