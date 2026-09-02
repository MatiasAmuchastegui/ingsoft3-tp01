# Joyería · Sistema de gestión de stock

[![CI](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/workflows/ci.yml/badge.svg)](https://github.com/MatiasAmuchastegui/ingsoft3-tp01/actions/workflows/ci.yml)

Sistema de gestión de stock para una joyería con tres locales. Se levanta entero con dos
comandos y no requiere instalar .NET, Node ni PostgreSQL.

---

## 1 · Puesta en marcha desde una máquina limpia

### Lo único que hace falta instalado

**Docker Desktop**, corriendo. Nada más — ni .NET, ni Node, ni PostgreSQL: todo eso viaja
dentro de las imágenes.

Comprobá que el motor está levantado antes de seguir:

```bash
docker info
```

Si responde con datos del servidor, está listo. Si dice `cannot find the file specified` o
`daemon is not running`, abrí Docker Desktop y esperá a que diga **Engine running**.

### Paso 1 — Traer el código

```bash
git clone https://github.com/MatiasAmuchastegui/ingsoft3-tp01.git
cd ingsoft3-tp01
```

### Paso 2 — Crear el archivo de configuración

```bash
cp .env.example .env
```

`.env.example` es la plantilla, está versionada y **sus valores funcionan tal como vienen**:
no hay que editar nada para verlo andar. `.env` es el archivo que Compose lee de verdad, y
está en `.gitignore` porque en un despliegue real lleva contraseñas.

> En Windows con PowerShell, `cp` funciona igual (es alias de `Copy-Item`).

### Paso 3 — Levantar el sistema

```bash
docker compose up -d
```

La primera vez tarda unos minutos: compila las dos imágenes. Después arranca en segundos.

### Paso 4 — Comprobar que arrancó bien

```bash
docker compose ps
```

Los tres servicios tienen que decir **`healthy`**. Si `backend` dice `starting`, esperá unos
segundos: está aplicando las migraciones y cargando los datos de ejemplo. El orden no es
casual — el backend no arranca hasta que PostgreSQL acepta conexiones de verdad.

Si algo no levanta:

```bash
docker compose logs -f backend
```

### Paso 5 — Entrar al sistema

Abrí **<http://localhost:8080>** en el navegador.

| Email | Contraseña | Rol | Qué ve |
|---|---|---|---|
| `admin@joyeria.local` | `Admin123!` | Admin | Los 3 locales. Administra el catálogo y transfiere entre locales |
| `vendedor1@joyeria.local` | `Vendedor123!` | Vendedor | Sólo Sucursal Centro |
| `vendedor2@joyeria.local` | `Vendedor123!` | Vendedor | Sólo Sucursal Nueva Córdoba |
| `vendedor3@joyeria.local` | `Vendedor123!` | Vendedor | Sólo Sucursal Shopping |

Esos usuarios los crea el seed la primera vez que arranca con la base vacía. Las contraseñas
salen del `.env`.

### Paso 6 — Detener el sistema

```bash
docker compose down       # apaga y CONSERVA los datos
docker compose down -v    # apaga y BORRA los datos
```

La diferencia está en el volumen. `down` destruye los contenedores, pero los datos viven en
un volumen con nombre (`db-data`) que sobrevive: al volver a levantar está todo. `down -v`
borra también ese volumen, y en el próximo arranque el seed rehace la base desde cero.

Para volver a levantarlo después, alcanza con `docker compose up -d` — el `.env` ya está.

### Alternativa: levantarlo sin compilar nada

Las imágenes están publicadas en ghcr.io, así que el sistema se puede correr **sin el código
fuente**:

```bash
cp .env.example .env
docker compose -f docker-compose.registry.yml up -d
```

Ese archivo no tiene ni una línea `build:`. Baja exactamente las mismas imágenes que se
probaron, bit a bit, en lugar de recompilarlas.

### Si algo falla

| Síntoma | Causa y solución |
|---|---|
| `failed to connect to the docker API` | Docker Desktop no está corriendo. Abrilo y esperá a *Engine running* |
| `port is already allocated` | Algo más usa el 8080. Cambiá `PUERTO_FRONTEND` en el `.env` y volvé a hacer `up -d` |
| `Filename too long` al clonar (Windows) | Límite de 260 caracteres por ruta. Corré `git config --global core.longpaths true` y volvé a clonar |
| El navegador muestra la página pero sin datos | El backend todavía está arrancando. Mirá `docker compose ps` |

---

## 2 · Qué es esta aplicación

Un sistema para llevar el inventario de una joyería con **tres locales físicos**. La idea
central, y la que ordena todo el diseño, es que **el stock es siempre por local: no existe un
stock global**. La pregunta que el sistema tiene que poder responder no es "¿cuántos anillos
tengo?" sino "¿cuántos anillos hay en el Shopping?".

Es un proyecto real —lo va a usar la dueña de un negocio— y a la vez la aplicación del
semestre de **Ingeniería de Software 3 (UCC, 2026)**, sobre la que cada trabajo práctico
agrega una capa.

| | |
|---|---|
| **Backend** | ASP.NET Core 8 Web API (C#) + Entity Framework Core 8 |
| **Frontend** | React 18 + Vite + TypeScript, servido por nginx |
| **Base de datos** | PostgreSQL 16 |
| **Arquitectura** | Monolito. Un repo, `backend/` y `frontend/` separados |
| **Autenticación** | JWT propio con roles Admin / Vendedor |

### Qué se puede hacer

| Pantalla | Qué hace |
|---|---|
| **Login** | Ingreso con email y contraseña. Devuelve un token que dura lo que diga la configuración |
| **Stock** | Existencias por local, con búsqueda y filtro de stock bajo. Desde cada fila se registra una entrada, una salida o una venta, y un Admin puede transferir a otro local. Debajo, los últimos movimientos |
| **Catálogo** | Alta, baja y modificación de productos y categorías, en dos pestañas. Sólo lectura para los vendedores |

Un Admin ve un selector con los tres locales. Un vendedor ve el suyo fijo, y la API le
rechaza cualquier consulta u operación sobre otro — no es que la pantalla se lo esconda: el
backend no se lo permite.

### Las dos ideas del modelo de datos

**El libro mayor es la verdad.** Cada variación de existencias deja un asiento en
`Movimiento`, que es *append-only*: no se edita ni se borra nunca. Si un movimiento fue un
error, se registra el movimiento contrario. Así queda una auditoría completa de quién movió
qué, cuándo y por qué.

**`Stock` es sólo una foto.** La cantidad por producto y local existe para poder consultarla
rápido, pero se modifica únicamente desde `MovimientoService`, dentro de la misma transacción
que inserta el asiento. Nunca a mano.

De ahí sale que las transferencias entre locales sean **atómicas**: una transferencia son dos
asientos —la salida de un local y la entrada al otro— creados juntos y compartiendo un mismo
`TransferenciaId`. O entran los dos, o no entra ninguno. Nunca puede quedar mercadería
"en el aire" entre dos locales.

### Los códigos de producto

El SKU lo **genera el sistema**, no se escribe a mano: cada categoría tiene un prefijo y se le
puede sumar un código de línea o marca. Relojes (`REL`) de la marca Citizen (`CT`) quedan
`RELCT-0001`, `RELCT-0002`, y así. Es único en todo el sistema y no se puede cambiar después,
porque va impreso en la etiqueta de la pieza física.

### Reglas de negocio

Viven en la capa de servicios y no en los controllers, para que se puedan probar sin levantar
HTTP.

| # | Regla | Dónde vive |
|---|---|---|
| 1 | El SKU de un producto es único en todo el sistema | `GeneradorSku` + `ProductoService` + índice único `ix_productos_sku` |
| 2 | El stock de un local nunca queda negativo | `MovimientoService` + `CHECK (cantidad >= 0)` |
| 3 | No se elimina una categoría que tiene productos | `CategoriaService` + FK `ON DELETE RESTRICT` |
| 4 | Una transferencia mueve stock entre locales de forma atómica | `MovimientoService.TransferirAsync` + `TransferenciaId` compartido |
| 5 | Un vendedor sólo ve y opera su local; un admin, todos | `AlcanceLocales` |

Reglas adicionales que surgieron del modelo:

- Un producto dado de baja no admite movimientos nuevos.
- Un vendedor necesita local asignado; un admin no debe tener ninguno.
- Un movimiento con cantidad cero o negativa se rechaza.
- Un asiento de transferencia no puede crearse suelto: sólo nace desde `TransferirAsync`.
- Un producto no se borra nunca si tiene historial: se desactiva (baja lógica).

Cada regla se verifica **dos veces**: en el servicio, que da el mensaje legible, y en la base,
que es la garantía real. La duplicación es deliberada — entre el `SELECT` que verifica y el
`INSERT` que escribe hay una ventana en la que otra operación puede meterse, y sólo la
restricción de la base la cierra.

### Qué falta para el uso real

El sistema anda, pero antes de dejarlo en manos de la dueña faltan cosas. Están anotadas a
propósito, no son un olvido:

| Falta | Por qué importa |
|---|---|
| **Copia de seguridad** | Los datos viven en un volumen de Docker en esa PC. Si se rompe el disco, se pierde el inventario. Hace falta un `pg_dump` programado a otro medio. **Es el más urgente: los otros tres son incomodidades, éste es pérdida de datos** |
| ABM de locales | Los tres locales los crea el seed. No hay forma de ponerles los nombres reales desde la aplicación |
| ABM de usuarios | El primer administrador se crea desde el `.env`; el segundo usuario habría que crearlo a mano en la base |
| Cambio de contraseña | Se fija en el `.env` y no se puede cambiar desde la aplicación |

---

## 3 · Cómo está armado por dentro

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

### Los dos archivos de compose

| Archivo | Para qué | Publica |
|---|---|---|
| `docker-compose.yml` | Sistema completo, **compilando** desde el código | `8080` (frontend) |
| `docker-compose.registry.yml` | Sistema completo, **bajando** las imágenes del registry | `8080` (frontend) |

### Estructura de carpetas

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
├─ .github/workflows/ci.yml    pipeline: construye las dos imágenes en cada PR
├─ scripts/
│  ├─ probar-reglas.ps1        76 verificaciones de las reglas de negocio contra la API
│  └─ probar-compose.ps1       persistencia, aislamiento de red y ruteo de la SPA
├─ docker-compose.yml          sistema completo (build local)
├─ docker-compose.registry.yml sistema completo (imágenes del registry)
├─ .env.example                plantilla de configuración (SÍ se commitea)
├─ decisiones.md               por qué cada cosa es como es, por TP
├─ evidencias.md               capturas y salidas reales
└─ README.md
```

---

## 4 · Configuración

Nada sensible está fijo en el código. En contenedores todo entra por el `.env` que lee el
compose; fuera de contenedores, por variables de entorno con el doble guión bajo de .NET.

| Variable del `.env` | Para qué |
|---|---|
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | Credenciales de la base |
| `JWT_KEY` | Clave de firma del token (**mínimo 32 bytes**) |
| `JWT_MINUTOS_DE_VIDA` | Duración de la sesión |
| `SEED_EMAIL_ADMIN` / `SEED_NOMBRE_ADMIN` | Datos del primer administrador |
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
arranca y dice por qué. Un error de configuración tiene que ser ruidoso y no aparecer recién
en el primer login.

Dos interruptores más, que el compose pone en `true` y en un despliegue real van en `false`:

| Variable | Qué hace |
|---|---|
| `AplicarMigracionesAlArrancar` | Aplica las migraciones pendientes al iniciar |
| `SembrarDatosIniciales` | Carga locales, productos y usuarios de ejemplo |

En `Development` los dos valen `true` por omisión.

---

## 5 · Desarrollo (sin Docker para la app)

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
# 1. Sólo la base, en contenedor (una vez; después alcanza con `docker start joyeria-db`)
docker run -d --name joyeria-db -p 5432:5432 -e POSTGRES_DB=joyeria -e POSTGRES_USER=joyeria -e POSTGRES_PASSWORD=joyeria_dev -v joyeria-db-dev:/var/lib/postgresql/data postgres:16-alpine

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

## 6 · Verificar que todo anda

Dos scripts de PowerShell, con el sistema levantado:

```powershell
# 76 verificaciones de las reglas de negocio, a través de nginx
.\scripts\probar-reglas.ps1 -BaseUrl http://localhost:8080

# Las mismas, contra el backend local sin Docker
.\scripts\probar-reglas.ps1

# Persistencia del volumen, aislamiento de red y ruteo de la SPA
.\scripts\probar-compose.ps1
```

> `probar-compose.ps1` hace `docker compose down -v`, así que **borra los datos de la base**.
> Es a propósito: comprobar la persistencia exige destruirla.

No son los tests del TP5 — esos van con xUnit dentro de `backend/JoyeriaStock.Tests/`. Estos
son verificaciones de caja negra contra la API corriendo.

---

## 7 · Comandos útiles

```bash
# ---- Docker ----
docker compose up -d --build          # reconstruir y levantar
docker compose ps                     # estado y salud de cada servicio
docker compose logs -f backend        # seguir los logs de la API
docker compose exec db psql -U joyeria -d joyeria
docker compose down                   # apagar, conservando datos
docker compose down -v                # apagar, borrando datos
docker volume ls                      # ver el volumen de la base
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

## Documentación del proyecto

- **[`decisiones.md`](decisiones.md)** — por qué cada cosa es como es, con una sección por
  trabajo práctico: los criterios de elección, los problemas que aparecieron y cómo se
  resolvieron, y la declaración de uso de IA.
- **[`evidencias.md`](evidencias.md)** — capturas y salidas reales de terminal que respaldan
  lo anterior.
