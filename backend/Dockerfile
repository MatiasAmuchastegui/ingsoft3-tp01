# Dockerfile multi-stage del backend.
#
# Por qué multi-stage: el SDK de .NET pesa cerca de 800 MB porque trae compilador,
# NuGet y todo lo necesario para BUILDEAR. Para EJECUTAR sólo hace falta el runtime.
# La etapa 1 compila con el SDK; la etapa 2 arranca de una imagen limpia y copia
# únicamente el resultado publicado. Nada del SDK queda en la imagen final.
#
# Build:  docker build -t joyeria-backend ./backend

# ---------------------------------------------------------------------------
# Etapa 1 — compilar
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primero SÓLO el .csproj y después restore. Si el .csproj no cambió, Docker reutiliza
# esta capa y no vuelve a bajar los paquetes NuGet: es la diferencia entre un rebuild
# de 4 segundos y uno de 40. Si copiáramos todo el código antes del restore, cualquier
# cambio en un .cs invalidaría la caché de los paquetes.
COPY JoyeriaStock.Api/JoyeriaStock.Api.csproj JoyeriaStock.Api/
RUN dotnet restore JoyeriaStock.Api/JoyeriaStock.Api.csproj

# Ahora sí el código fuente.
COPY JoyeriaStock.Api/ JoyeriaStock.Api/

RUN dotnet publish JoyeriaStock.Api/JoyeriaStock.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# ---------------------------------------------------------------------------
# Etapa 2 — ejecutar
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# curl es exclusivamente para el healthcheck del compose: la imagen aspnet no lo trae.
# Cuesta unos pocos MB y es lo que permite que "condition: service_healthy" funcione
# también para el backend, no sólo para la base.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# Usuario sin privilegios que la imagen base de .NET 8 ya define (APP_UID).
# Un proceso que no necesita root no corre como root.
USER $APP_UID

# EXPOSE es DOCUMENTACIÓN: declara que la aplicación escucha en 8080 dentro de la red
# de contenedores. NO publica nada en el host — eso se hace con "ports:" en el compose.
# Las imágenes de .NET 8 ya traen ASPNETCORE_HTTP_PORTS=8080 por defecto.
EXPOSE 8080

# ENTRYPOINT y no CMD: el contenedor ES esta aplicación, no un shell donde se corre algo.
# Con ENTRYPOINT, lo que se pase en "docker run imagen ..." llega como argumentos a
# la app; con CMD lo reemplazaría por completo.
ENTRYPOINT ["dotnet", "JoyeriaStock.Api.dll"]
