# syntax=docker/dockerfile:1
# Build context: src/web (see docker-compose.yml).

FROM node:24-alpine AS build
WORKDIR /app

# Install dependencies first, so this layer stays cached until package-lock.json changes.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.30-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/web/browser /usr/share/nginx/html
EXPOSE 8080
