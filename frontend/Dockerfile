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
# Etapa 2 — correr los tests
# ---------------------------------------------------------------------------
# Parte de "build", que ya tiene Node y las dependencias instaladas. Funciona porque el
# `npm ci` de arriba NO lleva --omit=dev: si lo llevara, vitest no estaría acá.
#
# Va ENTRE build y la etapa de nginx. Si estuviera al final, la imagen de producción
# terminaría siendo el contenedor de tests en lugar de nginx — y nada se pondría rojo.
#
# El reporte de cobertura sale a donde diga COVERAGE_DIR, que el pipeline apunta a una
# carpeta montada. Sin esa variable, el mismo comando escribe en coverage/ y sirve en
# la máquina de uno: una sola receta para los dos lados.
FROM build AS test
ENTRYPOINT ["npm", "run", "test:ci"]

# ---------------------------------------------------------------------------
# Etapa 3 — servir
# ---------------------------------------------------------------------------
FROM nginx:1.27-alpine AS final

# La configuración va como PLANTILLA en templates/, no como archivo en conf.d/. El
# entrypoint de la imagen oficial procesa lo que encuentre acá, reemplaza las ${VARIABLE}
# por su valor del entorno, y deja el resultado en conf.d/ antes de arrancar nginx.
#
# Así la dirección del backend no queda horneada en la imagen: la MISMA imagen sirve en
# cualquier entorno, y cada uno le pasa la suya. Es lo que hace posible promover la
# imagen verificada en lugar de reconstruirla por entorno.
COPY default.conf.template /etc/nginx/templates/default.conf.template
COPY --from=build /app/dist /usr/share/nginx/html

# Por defecto, los valores del docker-compose del TP2: el nombre del servicio y el DNS
# interno de Docker. Así `docker compose up` sigue funcionando sin tocar nada, y un
# entorno de nube los pisa con los suyos.
ENV BACKEND_URL=http://backend:8080 \
    DNS_RESOLVER=127.0.0.11

# Acota qué variables reemplaza el entrypoint. Sin esto sustituye TODAS las del entorno,
# y si alguna se llamara igual que una variable de nginx —$host, $scheme, $uri— la
# plantilla saldría con ese valor pegado y la configuración quedaría rota de una forma
# muy difícil de leer.
ENV NGINX_ENVSUBST_FILTER="^(BACKEND_URL|DNS_RESOLVER)$"

# Documentación del puerto interno. El puerto del host lo define "ports:" en el compose.
EXPOSE 80

# La imagen base de nginx ya trae su propio CMD y su manejo de señales; no se toca.
