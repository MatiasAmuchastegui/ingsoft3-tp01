# Joyería · Sistema de gestión de stock

Gestión de stock para una joyería con **3 locales físicos**. El stock es siempre por local:
no existe un stock global.

Es un proyecto real (lo va a usar la dueña del negocio) y a la vez la aplicación del semestre
de **Ingeniería de Software 3 (UCC, 2026)**, sobre la que cada trabajo práctico agrega una capa.

| | |
|---|---|
| **Backend** | ASP.NET Core 8 Web API (C#) + Entity Framework Core 8 |
| **Frontend** | React 18 + Vite + TypeScript, servido por nginx |
| **Base de datos** | PostgreSQL 16 |
| **Arquitectura** | Monolito. Un repo, `backend/` y `frontend/` separados |
| **Autenticación** | JWT propio con roles Admin / Vendedor |

---

## Levantar el sistema completo (2 pasos)

Lo único que hace falta instalado es **Docker**.

```bash
cp .env.example .env
docker compose up -d
```

Y listo: **<http://localhost:8080>**

Los valores de `.env.example` funcionan tal como vienen, así que no hay nada que configurar
para verlo andar. Entrá con:

| Email | Contraseña | Rol | Alcance |
|---|---|---|---|
| `admin@joyeria.local` | `Admin123!` | Admin | Los 3 locales. Administra el catálogo |
| `vendedor1@joyeria.local` | `Vendedor123!` | Vendedor | Sólo Sucursal Centro |
| `vendedor2@joyeria.local` | `Vendedor123!` | Vendedor | Sólo Sucursal Nueva Córdoba |
| `vendedor3@joyeria.local` | `Vendedor123!` | Vendedor | Sólo Sucursal Shopping |

Para apagarlo:

```bash
docker compose down       # conserva los datos (el volumen queda)
docker compose down -v    # borra los datos (elimina el volumen)
```

> En Windows con PowerShell, `cp` funciona igual (es alias de `Copy-Item`).

> **Si clonás en una carpeta muy profunda y Windows te dice `Filename too long`:** el límite
> histórico de 260 caracteres por ruta. La ruta más larga de este repo mide 104, así que hay
> lugar de sobra en una carpeta normal, pero si la tuya es larga, corré una vez
> `git config --global core.longpaths true` y volvé a clonar.

### Qué se levanta

```
                    ┌─────────────────────────────────────────────┐
  navegador ──8080──▶│ frontend  (nginx + la SPA compilada)        │
                    │   /            → index.html                  │
                    │   /api/*       → proxy al backend            │
                    └──────────────────────┬──────────────────────┘
                                red "publica"│
                    ┌──────────────────────▼──────────────────────┐
                    │ backend  (ASP.NET Core, puerto 8080 interno) │
                    └──────────────────────┬──────────────────────┘
                                red "interna"│
                    ┌──────────────────────▼──────────────────────┐
                    │ db  (PostgreSQL 16 + volumen db-data)        │
                    └─────────────────────────────────────────────┘
```

**El único puerto publicado al host es el 8080 del frontend.** El backend y la base no son
alcanzables desde la máquina: viven en redes internas de Docker y se hablan por nombre de
servicio. El frontend tampoco llega a la base — está en otra red.

Por eso el frontend **no necesita CORS**: el navegador le pide `/api/...` a nginx, que es el
mismo origen, y nginx lo reenvía al backend.

Para inspeccionar algo que no está publicado:

```bash
docker compose exec db psql -U joyeria -d joyeria    # la base
docker compose logs -f backend                        # los logs de la API
docker compose ps                                     # estado y healthchecks
```

---

## Instalarlo en la joyería

El inventario se maneja desde **una sola computadora**: la del dueño. Los tres locales no
tienen terminal propia. Opcionalmente, una segunda máquina se conecta a la primera.

> **El stock sigue siendo por local**, y no es una contradicción: una cosa es *dónde se
> carga* el dato (una computadora) y otra *a qué local pertenece* la mercadería (tres
> locales). El dueño registra desde su PC que en la Sucursal Centro entraron 5 anillos y en
> el Shopping se vendieron 2. Si el stock fuera global, el sistema no podría responder
> "¿cuántos anillos hay en el Shopping?", que es justamente para lo que sirve.

### Escenario A — todo en la computadora del dueño

```powershell
cp .env.example .env      # editar antes: contraseñas y datos del dueño
docker compose up -d
```

Y se usa en <http://localhost:8080>. No hace falta nada más.

### Escenario B — una PC hace de servidor y otra se conecta

En la PC servidor, lo mismo de arriba. Después:

1. **Averiguar su IP en la red local:**

   ```powershell
   ipconfig | Select-String "IPv4"
   ```

   Algo tipo `192.168.1.13`.

2. **Abrir el puerto en el Firewall de Windows** (una sola vez, como administrador):

   ```powershell
   New-NetFirewallRule -DisplayName "Joyeria Stock" -Direction Inbound `
       -Protocol TCP -LocalPort 8080 -Action Allow -Profile Private
   ```

   `-Profile Private` limita la regla a redes marcadas como privadas: no queda abierto en
   una red pública como la de un bar.

3. **Desde la segunda computadora**, entrar a `http://192.168.1.13:8080` con la IP del
   servidor. No hay nada que instalar: alcanza con un navegador.

4. **Que Docker Desktop arranque solo:** *Settings → General → Start Docker Desktop when
   you sign in*. Los contenedores tienen `restart: unless-stopped`, así que vuelven solos
   después de un reinicio o un corte de luz.

> Funciona por IP sin configurar nada porque el frontend le pide `/api` a nginx de forma
> **relativa**, y nginx reenvía al backend. Si la URL de la API estuviera compilada dentro
> del bundle, habría que reconstruir la imagen con la IP del dueño adentro.

Conviene fijar la IP del servidor en el router (reserva por MAC), o después de un reinicio
del router la segunda máquina apunta a una dirección que ya no es.

### Antes de entregárselo al dueño

Editar el `.env` con los datos reales. Como todavía no hay ABM de usuarios, **el
administrador se crea acá y sólo la primera vez**, con la base vacía:

```ini
SEED_EMAIL_ADMIN=eldueño@sumail.com
SEED_NOMBRE_ADMIN=Nombre del dueño
SEED_PASSWORD_ADMIN=una-contraseña-que-elija-él
POSTGRES_PASSWORD=otra-distinta
JWT_KEY=una-cadena-larga-y-al-azar-de-32-bytes-o-mas
```

Faltan cosas para el uso real, y son decisión consciente, no olvido:

| Falta | Por qué importa |
|---|---|
| **Copia de seguridad** | Los datos viven en un volumen de Docker en esa PC. Si se rompe el disco, se pierde el inventario. Hace falta un `pg_dump` programado a otro medio. **Es el más urgente: los otros tres son incomodidades, éste es pérdida de datos** |
| ABM de locales | Los tres locales los crea el seed. El dueño no tiene cómo ponerles los nombres reales |
| ABM de usuarios | El segundo usuario hay que crearlo a mano en la base |
| Cambio de contraseña | Se fija en el `.env` y no se puede cambiar desde la aplicación |

---

## Levantar desde el registry (sin compilar nada)

Las imágenes están publicadas, así que se puede correr el sistema **sin el código fuente**:

```bash
cp .env.example .env
docker compose -f docker-compose.registry.yml up -d
```

Ese archivo no tiene ni una línea `build:`. Es la misma imagen que se probó, bit a bit —
no una recompilación.

---

## Desarrollo (sin Docker para la app)

Para escribir código conviene tener el backend y el frontend corriendo en la máquina, con
recarga en caliente, y sólo la base en contenedor.

### Requisitos

| Herramienta | Versión | Verificar con |
|---|---|---|
| .NET SDK | 8.0.x | `dotnet --version` |
| Node.js | 20 o superior | `node --version` |
| Docker Desktop | reciente, **corriendo** | `docker info` |

En Windows, si falta alguna:

```powershell
winget install --id Microsoft.DotNet.SDK.8
winget install --id OpenJS.NodeJS.LTS
winget install --id Docker.DockerDesktop
```

Después de instalar, abrí una terminal nueva para que tome el `PATH`. La herramienta de
migraciones se instala una sola vez:

```powershell
dotnet tool install --global dotnet-ef --version 8.*
```

### Los tres pasos

```powershell
# 1. Sólo la base, en contenedor
docker compose -f docker-compose.dev.yml up -d

# 2. Backend (una terminal)
cd backend\JoyeriaStock.Api
dotnet run                      # http://localhost:5080 · Swagger en /swagger

# 3. Frontend (otra terminal)
cd frontend
copy .env.example .env
npm install
npm run dev                     # http://localhost:5173
```

En `Development` el backend aplica las migraciones y carga los datos de ejemplo solo al
arrancar, así que no hay un paso de migración aparte. Si preferís hacerlo explícito:
`dotnet ef database update`.

> Acá sí hace falta CORS, porque el frontend (5173) y la API (5080) son orígenes distintos.
> Ya está configurado. Es una de las razones por las que en contenedores se usa el proxy de
> nginx: elimina el problema en lugar de configurarlo dos veces.

---

## Los tres archivos de compose

| Archivo | Para qué | Publica |
|---|---|---|
| `docker-compose.yml` | Sistema completo, compilando desde el código | `8080` (frontend) |
| `docker-compose.registry.yml` | Sistema completo, bajando las imágenes del registry | `8080` (frontend) |
| `docker-compose.dev.yml` | Sólo PostgreSQL, para desarrollo local | `5432` (base) |

---

## Pantallas

| Pantalla | Qué hace |
|---|---|
| **Login** | Ingreso con email y contraseña |
| **Stock** | Listado por local con búsqueda y filtro de stock bajo. Desde cada fila se registra entrada, salida o venta. Debajo, los últimos movimientos |
| **Catálogo** | ABM de productos y categorías en dos pestañas. Sólo lectura para vendedores |

Un Admin ve un selector con los 3 locales; un Vendedor ve el suyo fijo y la API le rechaza
cualquier consulta u operación sobre otro.

---

## Reglas de negocio

Están en la capa de servicios, no en los controllers, para que se puedan testear sin levantar HTTP.

| # | Regla | Dónde vive |
|---|---|---|
| 1 | El SKU de un producto es único | `ProductoService` + índice único `ix_productos_sku` |
| 2 | El stock nunca queda negativo | `MovimientoService` + `CHECK (cantidad >= 0)` |
| 3 | No se elimina una categoría con productos | `CategoriaService` + FK `ON DELETE RESTRICT` |
| 4 | Descuento mayorista al alcanzar la cantidad umbral | `CalculadoraPrecio` (función pura) |
| 5 | Un vendedor sólo ve y opera su local; un admin, todos | `AlcanceLocales` |

Reglas adicionales que surgieron del modelo:

- Un producto dado de baja no admite movimientos nuevos.
- Un vendedor necesita local asignado; un admin no debe tener ninguno.
- No se puede configurar un descuento mayorista sin cantidad mínima.
- Un movimiento con cantidad cero o negativa se rechaza.

Cada regla se verifica **dos veces**: en el servicio, que da el mensaje legible, y en la base,
que es la garantía real. La duplicación es deliberada: entre el `SELECT` que verifica y el
`INSERT` que escribe hay una ventana en la que otra operación puede meterse, y sólo la
restricción de la base la cierra.

---

## Estructura

```
.
├─ backend/
│  ├─ Dockerfile               multi-stage: sdk compila → aspnet ejecuta
│  ├─ .dockerignore
│  ├─ JoyeriaStock.Api/
│  │  ├─ Domain/               entidades, enums, excepciones. Sin dependencias
│  │  ├─ Application/          servicios (las reglas de negocio), DTOs, abstracciones
│  │  ├─ Infrastructure/       DbContext, configuraciones EF, migraciones, seed, JWT
│  │  ├─ Controllers/          finos: validan HTTP y delegan al servicio
│  │  └─ Middleware/           traduce excepciones del dominio a códigos HTTP
│  └─ JoyeriaStock.Tests/      preparado y vacío: los tests son el TP5
├─ frontend/
│  ├─ Dockerfile               multi-stage: node compila → nginx sirve
│  ├─ nginx.conf               SPA + proxy /api al backend
│  ├─ .dockerignore
│  └─ src/
│     ├─ api/                  cliente HTTP y tipos espejo de los DTOs
│     ├─ auth/                 contexto de sesión
│     ├─ pages/                Login, Stock, Catálogo y sus formularios
│     └─ components/           Modal
├─ scripts/
│  ├─ probar-reglas.ps1        51 verificaciones de las reglas de negocio contra la API
│  └─ probar-compose.ps1       persistencia, aislamiento de red y ruteo de la SPA
├─ docker-compose.yml          sistema completo (build local)
├─ docker-compose.registry.yml sistema completo (imágenes del registry)
├─ docker-compose.dev.yml      sólo PostgreSQL, para desarrollo
├─ .env.example                plantilla de configuración (SÍ se commitea)
└─ README.md
```

---

## Configuración

Nada sensible está fijo en el código. En contenedores todo entra por el `.env` que lee el
compose; fuera de contenedores, por variables de entorno con el doble guión bajo de .NET.

| Variable del `.env` | Para qué |
|---|---|
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | Credenciales de la base |
| `JWT_KEY` | Clave de firma del token (**mínimo 32 bytes**) |
| `JWT_MINUTOS_DE_VIDA` | Duración de la sesión |
| `SEED_PASSWORD_ADMIN` / `SEED_PASSWORD_VENDEDOR` | Contraseñas de los usuarios de ejemplo |
| `PUERTO_FRONTEND` | Puerto del host donde se publica nginx |
| `IMAGE_PREFIX` / `IMAGE_TAG` | Nombre y tag de las imágenes en el registry |

**`.env` NO se commitea** (está en `.gitignore`). **`.env.example` sí**, y sus valores
funcionan tal cual para que clonar y levantar sean dos pasos.

Variables que lee el backend directamente (sin Docker):

```powershell
$env:ConnectionStrings__Default = "Host=mi-servidor;Database=joyeria;Username=app;Password=..."
$env:Jwt__Key = "una-clave-de-al-menos-32-bytes-de-largo"
dotnet run
```

`appsettings.json` trae esos dos valores **vacíos** a propósito: si faltan, la aplicación no
arranca y dice por qué. Un error de configuración tiene que ser ruidoso y no aparecer en el
primer login.

Dos interruptores más, que el compose pone en `true` y en un despliegue real van en `false`:

| Variable | Qué hace |
|---|---|
| `AplicarMigracionesAlArrancar` | Aplica las migraciones pendientes al iniciar |
| `SembrarDatosIniciales` | Carga locales, productos y usuarios de ejemplo |

En `Development` los dos valen `true` por omisión.

---

## Verificar que todo anda

Dos scripts de PowerShell, con el sistema levantado:

```powershell
# 51 verificaciones de las reglas de negocio, a través de nginx
.\scripts\probar-reglas.ps1 -BaseUrl http://localhost:8080

# Y contra el backend local, sin Docker
.\scripts\probar-reglas.ps1

# Persistencia del volumen, aislamiento de red y ruteo de la SPA
.\scripts\probar-compose.ps1
```

> `probar-compose.ps1` hace `docker compose down -v`, así que **borra los datos de la base**.
> Es a propósito: comprobar la persistencia exige destruirla.

No son los tests del TP5 — esos van con xUnit dentro de `backend/JoyeriaStock.Tests/`. Estos
son verificaciones de caja negra contra la API corriendo.

---

## Comandos útiles

```bash
# ---- Docker ----
docker compose up -d --build          # reconstruir y levantar
docker compose ps                     # estado y salud de cada servicio
docker compose logs -f backend        # seguir los logs de la API
docker compose exec db psql -U joyeria -d joyeria
docker compose down                   # apagar, conservando datos
docker compose down -v                # apagar, borrando datos
docker images                         # ver tamaños de las imágenes

# ---- Publicar en el registry ----
docker compose build
docker login ghcr.io -u <tu-usuario>  # con un token de GitHub como contraseña
docker compose push

# ---- Backend ----
dotnet build backend/JoyeriaStock.sln
cd backend/JoyeriaStock.Api
dotnet ef migrations add NombreDelCambio --output-dir Infrastructure/Migrations
dotnet ef migrations remove            # borrar la última, si no se aplicó
dotnet ef database drop --force && dotnet ef database update

# ---- Frontend ----
cd frontend
npm run lint                           # verificar tipos sin compilar
npm run build
```

> Ojo con `--no-build` en los comandos de `dotnet ef`: usa el último binario compilado. Si
> acabás de generar una migración, `dotnet ef database update --no-build` no la ve y responde
> "already up to date" sin aplicar nada.

---

## Estado por TP

| TP | Tema | Estado |
|---|---|---|
| TP1 | Git colaborativo | pendiente |
| TP2 | Contenedores | Dockerfiles, compose y registry listos; falta publicar las imágenes |
| TP3 | Planificación ágil | pendiente |
| TP4 | CI: pipelines as code | pendiente |
| TP5 | Tests + coverage | pendiente (`backend/JoyeriaStock.Tests/` ya está cableado, vacío) |
| TP6 | CD: environments y aprobaciones | pendiente |
| TP7 | Contenedores en el pipeline + e2e | pendiente |
| TP8 | Infraestructura como código | pendiente |
| TP9 | DevSecOps y observabilidad | pendiente |

Funcionalidad pendiente de la aplicación: transferencias entre locales, reportes y dashboard.
