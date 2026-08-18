# Dockerfile multi-stage del frontend.
#
# Por qué multi-stage acá: Node hace falta para COMPILAR (npm ci + vite build), pero el
# resultado son tres archivos estáticos. Servirlos no necesita Node: necesita un servidor
# web. La etapa 2 es nginx con el dist/ adentro — ni Node ni node_modules quedan en la
# imagen final, que pasa de ~1,1 GB a decenas de MB.
#
# Build:  docker build -t joyeria-frontend ./frontend

# ---------------------------------------------------------------------------
# Etapa 1 — compilar el bundle
# ---------------------------------------------------------------------------
FROM node:22-alpine AS build
WORKDIR /app

# Igual que en el backend: primero los manifiestos, después el código. Si las
# dependencias no cambiaron, Docker reutiliza la capa del npm ci.
# "npm ci" y no "npm install": instala exactamente lo que dice package-lock.json,
# sin resolver versiones de nuevo. Es lo que hace el build reproducible.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Vite congela las variables VITE_* dentro del bundle EN TIEMPO DE BUILD; no se pueden
# cambiar después arrancando el contenedor con otro valor.
#
# Por eso va vacía a propósito: vacío significa "mismo origen". El bundle pide
# /api/... y nginx lo reenvía al backend (ver nginx.conf). Así la imagen no queda
# atada a ninguna URL y la MISMA imagen sirve en QA y en producción — que es
# justamente el requisito de release inmutable del TP6/TP7.
ARG VITE_API_URL=""
ENV VITE_API_URL=$VITE_API_URL

RUN npm run build

# ---------------------------------------------------------------------------
# Etapa 2 — servir
# ---------------------------------------------------------------------------
FROM nginx:1.27-alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

# Documentación del puerto interno. El puerto del host lo define "ports:" en el compose.
EXPOSE 80

# La imagen base de nginx ya trae su propio CMD y su manejo de señales; no se toca.
