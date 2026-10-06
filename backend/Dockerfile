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
#
# También entra el .csproj del proyecto de tests: la etapa de tests parte de ÉSTA, así que
# si el proyecto no está acá adentro, `dotnet test` no lo encuentra — y lo peor es que no
# falla: una solución sin proyecto de tests devuelve 0 y no dice nada. El verde mentiría.
COPY JoyeriaStock.Api/JoyeriaStock.Api.csproj JoyeriaStock.Api/
COPY JoyeriaStock.Tests/JoyeriaStock.Tests.csproj JoyeriaStock.Tests/
COPY JoyeriaStock.sln .
RUN dotnet restore JoyeriaStock.sln

# Ahora sí el código fuente.
COPY JoyeriaStock.Api/ JoyeriaStock.Api/
COPY JoyeriaStock.Tests/ JoyeriaStock.Tests/

RUN dotnet publish JoyeriaStock.Api/JoyeriaStock.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# ---------------------------------------------------------------------------
# Etapa 2 — correr los tests
# ---------------------------------------------------------------------------
# Parte de "build" y no de una imagen nueva: los tests necesitan el SDK y el código fuente,
# que es exactamente lo que esa etapa ya tiene. Partir de ella no cuesta nada —Docker
# reutiliza esas capas— y garantiza que se testea el mismo código y las mismas dependencias
# que se compilaron.
#
# Va ENTRE build y final a propósito. Si estuviera al final del archivo, la última etapa
# pasaría a ser ésta y la imagen de producción terminaría siendo el contenedor de tests,
# sin que nada se ponga rojo.
#
# No lleva WORKDIR: hereda el /src de "build", que es donde está el código. Un WORKDIR fijo
# apuntaría a una carpeta vacía y dotnet test no encontraría la solución.
#
# ENTRYPOINT y no RUN: con RUN los tests correrían al CONSTRUIR la imagen, y sacar de ahí el
# reporte de cobertura pide otro mecanismo. Con ENTRYPOINT corren en el `docker run` del
# pipeline, y los resultados salen por un volumen montado en /out.
#
# Los cuatro /p: son el UMBRAL, y hacen falta los cuatro:
#
#   CollectCoverage   activa la medición de coverlet.msbuild. Sin esto el umbral se IGNORA
#                     en silencio: el job queda verde y no exige nada.
#   Threshold         el número elegido. Mide 18,2% de línea y 21,7% de rama hoy; 16 frena
#                     si baja sin ser inalcanzable.
#   ThresholdType     sobre qué métricas. Sin esto exige línea, rama Y MÉTODO a la vez, y se
#                     falla por método sin entender por qué. La coma va como %2c porque
#                     MSBuild parte los valores por coma — con una coma de verdad muere con
#                     MSB1006 antes de correr un solo test.
#   Exclude           QUÉ entra en la cuenta. Sin esto se mide el ensamblado entero —arranque,
#                     migraciones, entidades— y el umbral falla siempre por una razón que no
#                     tiene nada que ver con los tests.
#
# Esta lista tiene que decir LO MISMO que los -classfilters del ci.yml. Si no coincidieran,
# el resumen de la corrida diría un número y el umbral exigiría otro.
#
# El --collect de más abajo convive con esto y hace otra cosa: produce el archivo que lee el
# reporte. Estos /p: son los que FRENAN.
FROM build AS test
ENTRYPOINT ["dotnet", "test", "JoyeriaStock.sln", \
            "/p:CollectCoverage=true", \
            "/p:Threshold=16", \
            "/p:ThresholdType=line%2cbranch", \
            "/p:Exclude=[JoyeriaStock.Api]Program*%2c[JoyeriaStock.Api]JoyeriaStock.Api.Domain.Entities.*%2c[JoyeriaStock.Api]JoyeriaStock.Api.Application.Dtos.*%2c[JoyeriaStock.Api]JoyeriaStock.Api.Infrastructure.Migrations.*%2c[JoyeriaStock.Api]JoyeriaStock.Api.Infrastructure.Configurations.*%2c[JoyeriaStock.Api]JoyeriaStock.Api.Infrastructure.AppDbContext%2c[JoyeriaStock.Api]JoyeriaStock.Api.Infrastructure.DbSeeder*", \
            "--logger", "trx;LogFileName=tests.trx", \
            "--results-directory", "/out"]

# ---------------------------------------------------------------------------
# Etapa 3 — ejecutar
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
