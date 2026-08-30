# Decisiones — TP1

## 1. Por qué Git no pudo resolver el conflicto solo

Las ramas A y B partieron del mismo commit de `main` y las dos modificaron **la misma línea** del `README.md` (el título), cada una con un contenido distinto ("versión A" vs "versión B"). Cuando Git hace un merge de 3 vías compara cada rama contra el ancestro común: si una sola rama cambió esa línea, Git sabe qué versión "ganó" y la aplica solo. Pero acá **las dos** la cambiaron, y no hay forma de que Git sepa cuál de las dos versiones es la correcta — no es un problema técnico que un algoritmo pueda resolver, es una decisión de contenido que solo puede tomar una persona. Por eso Git se detiene, marca el archivo con `<<<<<<<`, `=======` y `>>>>>>>`, y me delega la decisión a mí.

Para que nunca hubiera aparecido, tendría que haber evitado que las dos ramas tocaran la misma línea al mismo tiempo: por ejemplo, mergeando la rama A y recién después creando la rama B (partiendo ya del `main` actualizado, con el título ya cambiado), en vez de crear las dos a la vez desde el mismo punto de partida. En un equipo real esto se traduce en ramas cortas, integración frecuente y comunicación sobre quién está tocando qué archivo — cuanto más tiempo vive una rama sin integrarse, más probable (y más grande) es el conflicto.

## 2. Problemas que encontré y cómo los solucioné

- **`Require approvals` quedó tildado por default.** Al activar "Require a pull request before merging" en la protección de `main`, GitHub tilda solo la casilla de "Require approvals" con el valor 1. Como el TP es individual y GitHub no te deja aprobar tu propio PR, esto me iba a dejar sin poder mergear nunca. Lo solucioné destildando esa opción para que quedara en cero aprobaciones obligatorias.

- **El botón de mergear no se habilitaba enseguida después de resolver el conflicto.** Después de sacar los marcadores del `README.md` en el editor web y tocar "Commit merge", el botón para mergear el PR de la rama B se quedó un rato sin habilitarse. Al principio pensé que mi resolución había quedado mal, pero era que GitHub estaba recalculando el estado del PR — esperando unos segundos se habilitó solo, sin que tuviera que tocar nada más.

- **La evidencia del push rechazado no aísla la causa.** Cuando probé pushear directo a `main`, el rechazo que me dio GitHub fue `[rejected] main -> main (fetch first)` — un rechazo normal por tener el `main` local desactualizado, no necesariamente el rechazo específico de la protección de rama (`protected branch hook declined`). No reintenté el push después de actualizar mi rama local, así que esta captura por sí sola no prueba de forma concluyente que la protección esté funcionando, aunque el resto de la configuración (branch protection activa, "Do not allow bypassing" activado) sí está verificada en la configuración del repositorio.

## 3. Declaración de uso de IA


Usé IA (Claude) durante el desarrollo del TP para entender conceptos de Git que la guía introduce — sobre todo cómo funciona un merge de 3 vías, la diferencia entre un rechazo por protección de rama y un rechazo por estar desactualizado, y por qué el reviewer no puede aprobar su propio PR en GitHub. Verifiqué esas explicaciones contrastándolas con el marco teórico de la guía de la cátedra (§3.4 y §3.6) y con lo que efectivamente veía en mi propio repositorio y mis propias capturas.

También usé Claude para redactar la versión final de este archivo y de `evidencias.md`, a partir de contarle qué había hecho, qué capturas tenía y qué problemas encontré. Verifiqué el resultado revisando cada captura contra la descripción que se le puso, y corrigiendo cuando la interpretación automática no coincidía con lo que realmente pasó — por ejemplo, la primera lectura de la captura del push rechazado asumía que probaba la protección de rama, y yo aclaré que el mensaje correspondía a un rechazo por estar desactualizado, no confirmado como rechazo por protección, lo cual quedó reflejado en el punto 2 de este documento.

---

# Decisiones — TP2

## 1. Qué app elegí y por qué

**Un sistema de gestión de stock para una joyería con tres locales.** Backend en ASP.NET Core 8
con Entity Framework Core, frontend en React + Vite + TypeScript, base PostgreSQL 16.

Es una aplicación **propia**, escrita para este semestre — no es un repo de GitHub adaptado ni un
trabajo grupal de otra materia, que es lo que la guía pide evitar. Además es un proyecto real: la
va a usar la dueña de una joyería, y eso le da reglas de negocio de verdad en lugar de inventadas.

Contra los criterios de la guía (§3.3):

| Criterio | Cómo lo cumple |
|---|---|
| **¿Buildea y corre localmente hoy?** | Sí, y por dos caminos probados: `docker compose up -d`, o sin Docker con `dotnet run` + `npm run dev` y sólo la base en contenedor |
| **¿Tiene o puedo escribirle tests?** | Las reglas de negocio están en la capa de servicios, no en los controllers, así que se testean sin levantar HTTP. El proyecto `JoyeriaStock.Tests` ya está cableado con xUnit, vacío a la espera del TP5 |
| **¿Entiendo el código para modificarlo?** | Sí, con el matiz honesto de la sección 5: lo escribí con asistencia de IA, revisando y verificando cada decisión |
| **Tamaño** | Tres pantallas: Login, Catálogo (dos pestañas) y Stock. La guía avisa que más grande no suma nota, sólo fricción |

**Dependencias**: sólo backend + frontend + PostgreSQL. Nada de Redis, colas ni APIs de terceros.

**Reglas de negocio disponibles para el TP5**: SKU generado por el sistema y correlativo · SKU
inmutable después del alta · categoría inmutable en un producto · prefijo único por categoría ·
prefijo inmutable si ya emitió códigos · el stock nunca queda negativo · no se elimina una
categoría con productos · un vendedor sólo ve y opera su propio local · transferencia entre
locales atómica · un vendedor no puede transferir · un producto dado de baja no admite
movimientos · el precio se congela en la venta.

## 2. Decisiones de contenerización

### Imágenes base

| | Etapa de build | Etapa final |
|---|---|---|
| Backend | `mcr.microsoft.com/dotnet/sdk:8.0` | `mcr.microsoft.com/dotnet/aspnet:8.0` |
| Frontend | `node:22-alpine` | `nginx:1.27-alpine` |

**Por qué multi-stage.** Compilar y ejecutar necesitan cosas distintas. El SDK de .NET pesa 1,2 GB
porque trae compilador, MSBuild y NuGet; para ejecutar alcanza el runtime, que son 320 MB. La
imagen final quedó en **344 MB**: dentro hay 11 MB de aplicación publicada (33 ensamblados) más
`curl`, y ni rastro del compilador ni del código fuente — menos peso y menos superficie de ataque.

En el frontend el efecto es todavía más marcado, porque **el resultado de compilar son tres
archivos estáticos**: 216 KB en total (188,9 KB de JavaScript y 5,7 KB de CSS). Servirlos no
necesita Node, así que la etapa final es nginx con el `dist/` adentro. Si hubiera dejado Node y
`node_modules`, la imagen habría rondado los 400 MB en lugar de 73,9 MB.

**Versiones fijas, no `latest`.** `sdk:8.0` y no `sdk:latest`, para que el build de hoy y el de
noviembre usen el mismo .NET. Usar `latest` es la forma más fácil de que un pipeline empiece a
fallar sin que nadie haya cambiado nada.

**Orden de las instrucciones.** En los dos Dockerfiles se copian primero los archivos de
dependencias (`.csproj`, `package.json` + `package-lock.json`), se restauran, y recién después se
copia el código. Así, cambiar una línea de código no vuelve a descargar todos los paquetes.
En el frontend uso `npm ci` y no `npm install`: instala exactamente lo que dice el lockfile, sin
volver a resolver versiones, que es lo que hace el build reproducible.

**Instalé `curl` en la imagen del backend.** La imagen `aspnet` no lo trae, y sin él el healthcheck
del compose no tiene con qué consultar `/health`. Sin healthcheck en el backend, el
`condition: service_healthy` del frontend no sirve de nada. Cuesta unos MB y lo compro.

**El backend corre como usuario sin privilegios** (`USER $APP_UID`, que la imagen base de .NET 8 ya
define). Un proceso que no necesita root no corre como root.

### nginx como proxy: el camino (a) de la guía

Mi SPA llama a `/api/...` con ruta **relativa**, y nginx reenvía eso al backend por la red interna.
Es la opción (a) de §2.6 de la guía, y la elegí por una razón concreta: **Vite congela las
variables `VITE_*` dentro del bundle en tiempo de build**. Si la URL de la API se hornea al
compilar, hacen falta imágenes distintas por entorno, y eso choca de frente con el requisito de
release inmutable del TP7 ("lo que se prueba en QA es exactamente lo que corre en producción").

Construyendo con `VITE_API_URL` vacía, la imagen no lleva ninguna URL adentro y sirve igual en
cualquier entorno. Beneficio lateral: como para el navegador todo sale del mismo origen, **no hay
CORS que configurar** en contenedores. Fuera de contenedores sí hace falta y está configurado,
pero el problema desaparece en lugar de resolverse dos veces.

Lo verifiqué buscando `localhost:5080` dentro del bundle de la imagen ya construida: no está, y la
llamada quedó compilada como `/api/auth/login`.

### `EXPOSE` contra `ports:`

`EXPOSE 8080` en el backend y `EXPOSE 80` en el frontend son **documentación**: declaran en qué
puerto escucha el proceso dentro de la red de contenedores. **No publican nada en el host.** Lo
único que publica es `ports:`, y hay un solo `ports:` en todo el compose: el del frontend.

Comprobado con el sistema levantado y sano: el `8080` responde desde la máquina, y el `5080` del
backend y el `5432` de la base **no**.

### Dos redes en lugar de una

```
publica : frontend <-> backend
interna : backend  <-> db
```

El backend está en las dos porque es el único que necesita hablar con ambos lados. La base está
sólo en `interna`, así que ni el frontend ni el host la alcanzan. El compose no lo exige y con una
sola red también funcionaría; son seis líneas y dan una propiedad real: el servicio expuesto no
tiene ruta hacia la base de datos.

### Migraciones y datos de ejemplo

El backend aplica las migraciones y carga los datos de ejemplo al arrancar, pero **sólo si dos
interruptores están en `true`** (`AplicarMigracionesAlArrancar` y `SembrarDatosIniciales`). El
compose del TP2 los activa, porque el requisito acá es que un solo comando deje el sistema usable.
En un despliegue real van en `false`: la migración es un paso del pipeline (TP6), y sembrar un
usuario admin con contraseña conocida en producción es un agujero.

El seed es idempotente: si ya hay locales cargados no hace nada, así que reiniciar no duplica datos.

## 3. Qué persiste y qué no

**Persiste**: los datos de PostgreSQL, en el volumen con nombre `db-data` montado en
`/var/lib/postgresql/data`.

**No persiste nada más**, y es a propósito: las tres imágenes son inmutables y los contenedores
descartables. No hay archivos subidos, ni caché en disco, ni logs escritos a un archivo — van a
stdout, que es lo que Docker espera y lo que el TP9 va a necesitar para centralizarlos.

| Comando | Contenedores | Volumen | Datos |
|---|---|---|---|
| `docker compose down` | se borran | **queda** | **sobreviven** |
| `docker compose down -v` | se borran | se elimina | se pierden |

La diferencia está en **dónde vive el dato**. Si PostgreSQL escribiera en el filesystem del
contenedor, `down` se llevaría todo, porque la capa de escritura de un contenedor muere con él. El
volumen es almacenamiento que Docker administra **aparte del ciclo de vida del contenedor**, y por
eso `down` no lo toca: hay que pedirlo explícitamente con `-v`.

Está verificado en `evidencias.md` creando un producto que no viene del seed y siguiéndolo a través
de los dos comandos.

## 4. Problemas que encontré y cómo los resolví

### El token de concurrencia sobre `xmin` no era viable

Para que dos ventas simultáneas no dejaran el stock negativo probé la concurrencia optimista sobre
la columna de sistema `xmin` de PostgreSQL. `UseXminAsConcurrencyToken()` rompió el build con
`CS0618: está obsoleto` — el proyecto compila con `TreatWarningsAsErrors`. Al configurarlo a mano
como indica la guía de migración de Npgsql 8, la migración generada creaba una columna llamada
`xmin`, que PostgreSQL rechaza porque es un nombre de columna de sistema reservado.

Lo encontré **leyendo el archivo de migración antes de aplicarlo**, no cuando explotó. Lo reemplacé
por un `CHECK (cantidad >= 0)` sobre la tabla `stocks`: cubre el mismo caso con menos maquinaria y
la garantía es más fuerte, porque vale también para un `UPDATE` escrito a mano en psql y no sólo
para lo que pasa por Entity Framework.

### `dotnet ef database update --no-build` dijo "already up to date" sin aplicar nada

Generé la migración, corrí `database update --no-build` y me contestó *"No migrations were applied.
The database is already up to date"*… con la base vacía, sin una sola tabla.

La causa: `--no-build` usa el último binario compilado. Como generé la migración **después** de
compilar, el DLL no la contenía; para EF no existía ninguna migración, así que efectivamente estaba
todo al día. Se arregla compilando entre generar y aplicar, o directamente no usando `--no-build`.
Quedó anotado en el README porque es de los errores que no dan ninguna pista.

### El healthcheck del frontend fallaba por IPv6, con nginx impecable

`docker compose ps` mostraba el frontend `unhealthy` **con la aplicación funcionando y todas las
verificaciones en verde**. El healthcheck usaba `http://localhost/nginx-health`; dentro del
contenedor `/etc/hosts` mapea `localhost` a `127.0.0.1` **y** a `::1`, busybox wget intentaba por
IPv6, y nginx escucha sólo en `0.0.0.0:80`. Resultado: `connection refused` para siempre.

Importaba más de lo que parece: el `depends_on: condition: service_healthy` que apunta al frontend
quedaba decorativo, y en el TP6 cualquier gate de despliegue basado en la salud del servicio nunca
habría aprobado. Se arregló poniendo `127.0.0.1` explícito en los healthchecks de los dos compose,
y usando sólo las opciones que busybox soporta (`--tries` es de GNU wget y no existe ahí).

El healthcheck del backend tuvo el problema hermano: fallaba porque la imagen `aspnet` no trae
`curl` ni `wget`. Por eso lo instalo en el Dockerfile. Son dos comandos distintos en cada
contenedor porque son dos imágenes base distintas.

### Los productos nuevos no aparecían en la pantalla de Stock

Este no lo encontró ningún test: apareció **usando la aplicación**. Cargaba una categoría
"Relojes", le daba de alta un reloj… y el producto no aparecía nunca en la pantalla de Stock. Y
como los movimientos se registran desde una fila de esa pantalla, no había forma de darle unidades:
quedaba invisible para siempre.

La causa era coherente con el diseño: una fila de `stocks` sólo nace cuando se registra el primer
movimiento de ese producto en ese local, y la consulta partía de esas filas. Los productos del seed
no mostraban el problema porque el seeder les crea las filas a mano. Lo resolví haciendo que la
consulta parta de **productos × locales** con `LEFT JOIN` contra `stocks`, mostrando 0 donde no hay
fila. Así se arregla también el caso simétrico —abrir un local nuevo— sin tener que crear filas
para todo el catálogo.

**La lección**: las verificaciones automatizadas estaban todas en verde mientras el bug existía,
porque todas partían de los datos del seed. Una batería verde no reemplaza usar el sistema.

### `tsc -b` fallaba con `TS6310`

`npm run build` cortaba con *"Referenced project may not disable emit"*. La plantilla de Vite parte
la configuración de TypeScript en dos proyectos con `references`, lo que obliga a `composite: true`,
y eso es incompatible con el `noEmit: true` del proyecto referenciado. Lo resolví con un solo
`tsconfig.json` que incluye `src` y `vite.config.ts`, y `tsc --noEmit && vite build`. La
verificación de tipos sigue siendo estricta y sigue corriendo antes del build, que es lo que el
gate de calidad del TP4 va a necesitar.

### `Filename too long` al clonar el repositorio

Probando el arranque como lo haría un corrector —clonar en una carpeta limpia y seguir el README—
el checkout falló con `Filename too long`: el límite histórico de 260 caracteres por ruta de
Windows. Medí: la ruta más larga del repositorio son 104 caracteres (un archivo de migración de EF)
y la carpeta donde estaba probando medía 160, así que sumaban 264. En una carpeta normal sobra
lugar, pero quedó documentado en el README con la solución (`git config --global core.longpaths true`).

### El tropiezo que la cátedra anticipa y no me pasó

La guía avisa que el error más común es dejar `localhost` en la cadena de conexión. No me pasó, y
vale la pena explicar por qué: **desde el principio la cadena de conexión fue una variable de
entorno y no un literal en el código**. Al pasar a contenedores sólo cambió el valor
(`Host=localhost` → `Host=db`), sin tocar una línea.

Adentro de un contenedor `localhost` es el contenedor mismo, no la máquina: el backend buscaría
PostgreSQL dentro de su propio contenedor y no lo encontraría. `db` es el nombre del servicio en el
compose, que Docker resuelve por DNS dentro de la red.

Lo mismo con el segundo tropiezo, el backend arrancando antes que la base: `depends_on` pelado sólo
ordena el arranque y sigue de largo. Hace falta `condition: service_healthy` para que Compose
espere a que `pg_isready` confirme que el motor acepta conexiones. El contenedor de PostgreSQL
arranca en un segundo; el motor tarda varios más.

## 5. Declaración de uso de IA

**Herramienta**: Claude (Claude Code), trabajando sobre este repositorio.

**Qué se hizo con IA**: prácticamente todo el código de la aplicación —entidades y configuraciones
de EF, los servicios con las reglas de negocio, los controllers, el middleware de errores, el
frontend completo— además de los dos Dockerfiles, los tres archivos de compose, el `nginx.conf`, y
la primera redacción de este documento, de `evidencias.md` y del README.

**Qué aporté yo**: la elección del dominio y del stack, el modelo de datos inicial, las reglas de
negocio, las restricciones de la cátedra, y las decisiones cuando había que elegir entre
alternativas (JWT propio en lugar de ASP.NET Identity, ghcr.io en lugar de Docker Hub, monolito en
lugar de microservicios, tres pantallas en lugar de cinco). También la conversación con la dueña de
la joyería sobre cómo trabajan de verdad, que cambió cosas del diseño, y las pruebas manuales que
encontraron el bug de los productos invisibles.

**Cómo lo verifiqué** — esto es lo que me permite defenderlo:

1. **Nada se dio por bueno porque compilara.** Hay 80 verificaciones automatizadas de las reglas de
   negocio contra la API corriendo, y se corrieron en tres contextos distintos: contra el backend
   local, contra el sistema en contenedores a través de nginx, y contra las imágenes bajadas del
   registry.
2. **El esquema de la base lo revisé en `psql`**, tabla por tabla, para confirmar que el `CHECK`, el
   índice único y las claves foráneas en `RESTRICT` existían de verdad y no sólo en el código.
3. **Leí cada migración antes de aplicarla.** Ahí encontré el problema de la columna `xmin`, que
   habría explotado en el primer `up` de otra persona.
4. **Inspeccioné el bundle compilado por dentro** para confirmar que no tenía la URL de la API
   horneada, en lugar de asumir que el build arg había funcionado.
5. **La persistencia la probé destruyéndola**: creando un dato que no viene del seed y siguiéndolo a
   través de `down`/`up` y de `down -v`/`up`.
6. **Usé la aplicación como usuario.** El bug de los productos invisibles no lo encontró ningún
   test: apareció cargando un reloj a mano desde la interfaz.
7. **Varias fallas resultaron ser de mis propios scripts de prueba y no del sistema** — por ejemplo
   un `400` que parecía un bug del backend y era que PowerShell mandaba el cuerpo sin declarar
   UTF-8, así que un nombre con tilde llegaba corrupto. Las corregí y las dejo anotadas porque
   distinguir "falla el sistema" de "falla mi prueba" fue parte del trabajo.

**Qué significa esto**: puedo explicar por qué cada decisión es como es, y este documento existe
para eso. Lo que no puedo decir es que escribí el código a mano.

---

# Decisiones — TP3

## 1. Duración del sprint y por qué

**Elegí una semana.**

El criterio no fue "qué duración es la mejor" en abstracto, sino **cuánto tarda en llegarme la
información que necesito para corregir el rumbo**. En esta materia el ciclo de realimentación es
semanal: hay una clase por semana, y entre clase y clase es cuando entiendo si lo que planifiqué
tenía sentido. Un sprint más largo que ese ciclo significa enterarme de que planifiqué mal cuando
ya no me queda margen para reaccionar.

Los tres factores que pesaron:

| Factor | Cómo empuja la decisión |
|---|---|
| Cadencia de la materia | Una clase por semana. El sprint alineado con ella cierra justo cuando llega la corrección |
| Tamaño del equipo | Uno solo. Con una persona, la ceremonia de un sprint largo no compra nada: no hay que sincronizar a nadie |
| Tamaño del trabajo | Cada TP entra cómodo en una semana. Dos semanas me obligarían a partir un TP al medio o a mezclar dos |

Lo que gano concretamente: si sobreestimé lo que entraba en el sprint, lo descubro en siete días y
no en catorce. El costo es más ceremonia por unidad de tiempo —cerrar y abrir sprint el doble de
veces— y lo acepto porque con una sola persona esa ceremonia son diez minutos.

Un sprint de dos semanas tendría sentido si el trabajo dependiera de terceros con tiempos de
respuesta largos, o si el equipo fuera lo bastante grande como para que la coordinación costara
más que la realimentación tardía. Ninguna de las dos cosas pasa acá.

**Sprint 1** arranca el 2026-08-30 y dura 7 días. Contiene la historia #11 y sus dos tareas, #12 y
#13. La épica #10 no entra a ningún sprint —es el paraguas del semestre entero— y el bug #14 queda
en el backlog, priorizado pero sin comprometer.

## 2. Límite de trabajo en progreso y por qué

**Puse 2 en la columna *In Progress*.**

El límite no está para medir productividad, está para **hacer visible el costo de empezar cosas
sin terminarlas**. Cuando tengo cuatro tarjetas abiertas, ninguna avanza: cada vez que cambio de
una a otra pago el costo de recordar dónde estaba, y el trabajo a medio hacer no le sirve a nadie
hasta que se termina. Un ítem sin terminar tiene valor cero.

Elegí **2** y no 1 ni 3 por una razón práctica: con 1 me quedo bloqueado cada vez que algo depende
de esperar —un run de CI que tarda, una revisión, algo que tengo que consultar—, y quedarme sin
hacer nada por respetar el tablero sería el tablero mandando sobre el trabajo. Con 2 tengo exactamente
una tarjeta a la que pasar cuando la primera se traba, y ni una más. Con 3 el límite deja de
apretar: nunca lo tocaría, y un límite que nunca se toca no informa nada.

La regla que se sigue del límite es la que importa: **cuando la columna está llena, no se empieza
nada nuevo — se ayuda a terminar lo que ya está ahí.** El límite convierte "estoy ocupado" en
"estoy trabado", que es una información distinta y mucho más útil.

Lo comprobé en el tablero: al arrastrar una tercera tarjeta a *In Progress*, GitHub marca la
columna en rojo y muestra el conteo por encima del límite, **pero deja pasar la tarjeta**. Es un
aviso, no un candado. Eso está bien: el límite es un acuerdo de trabajo, y si en un caso puntual
hay que romperlo, lo que uno quiere es verlo y decidirlo, no que la herramienta lo impida en
silencio.

Estado con el que quedó el tablero al cierre del sprint 1: la historia #11 y su tarea pendiente
#13 en *In Progress* —exactamente el límite—, la tarea #12 en *Done*, y la épica #10 y el bug #14
en *Todo*.

## 3. Diagnóstico de la historia mal escrita

El enunciado pide identificar por qué una historia como

> **Como** usuario **quiero** que el CI funcione bien **para** que todo ande

está mal escrita. Tiene tres problemas distintos, y conviene separarlos porque se arreglan de
maneras distintas.

**a) El rol no identifica a nadie.** "Usuario" es todo el mundo, y por lo tanto nadie. El rol
existe para poder preguntarle a alguien concreto si la historia le resuelve algo; si el rol es
genérico, no hay a quién preguntarle y la historia no se puede validar. En mi caso el rol es
*desarrollador*, que es quien abre los PR y a quien le sirve que el pipeline los verifique.

**b) El "quiero" describe un estado, no un comportamiento.** "Que funcione bien" no dice qué hace
el sistema. No se puede implementar porque no dice qué construir, y no se puede probar porque no
dice qué observar. La corrección es reemplazar el adjetivo por una acción visible: *que cada Pull
Request ejecute el build y los tests automáticamente*. Eso sí se puede mirar y decir sí o no.

**c) El "para" no expresa valor.** "Para que todo ande" es una repetición del "quiero" con otras
palabras, no una razón. La parte del *para* es la que permite decidir si la historia vale la pena
y priorizarla contra otra; si no dice qué mejora, no sirve para priorizar. La versión útil dice
qué evita: *para detectar regresiones antes del merge, y no después*.

**El problema de fondo es uno solo: la historia no es verificable.** No hay forma de pararse frente
a ella y decir "esto está hecho" sin que sea una opinión. Y una historia que no se puede dar por
terminada tampoco se puede estimar, ni priorizar, ni cerrar.

Así quedó la mía (#11), y la diferencia se ve en los criterios de aceptación, que son la prueba de
que la reescritura funcionó — cada uno se comprueba mirando algo, no discutiéndolo:

| Criterio | Cómo se comprueba |
|---|---|
| Corre en cada PR contra `main` | Se abre un PR y aparece el check en la página del PR |
| Un test que falla bloquea el merge | Se rompe un test a propósito: el botón de merge queda deshabilitado |
| El reporte queda como artefacto | En la pestaña Actions, el run tiene un artefacto descargable |
| El badge está en el README | Se ve en la portada del repositorio y cambia de color según el último run |

Y el mismo criterio de "que se pueda verificar" es el que separa los tres tipos de ítem que usé:

- La **épica** (#10) no lleva criterios de aceptación a propósito: no se verifica por sí misma, se
  cierra cuando sus historias están cerradas.
- La **historia** (#11) lleva criterios porque es la unidad que entrega valor observable.
- Las **tareas** (#12, #13) no llevan criterios sino una lista de trabajo concreto: son el *cómo*
  interno de la historia, y quien pide la historia no debería tener que leerlas.
- El **bug** (#14) va al costado de la jerarquía y no colgando de ninguna historia, porque es un
  defecto sobre algo ya entregado —el TP2— y no parte de un incremento nuevo. Por eso lleva pasos
  para reproducirlo, comportamiento observado y comportamiento esperado, que es lo que hace falta
  para arreglarlo.

## 4. Trazabilidad: la vuelta completa

La cadena que pide el práctico quedó cerrada de punta a punta, y se puede recorrer en los dos
sentidos sin salir de GitHub:

```
épica #10
  └── historia #11
        ├── tarea #12  (cerrada)  ──> PR #15  ──> commit 34ccaf1 en main
        └── tarea #13  (abierta)
```

- El PR #15 lleva `Closes #12` en el cuerpo.
- Al mergearlo, GitHub cerró el issue #12 **solo**: el merge quedó registrado a las 20:52:09 y el
  cierre del issue a las **20:52:10**, un segundo después. Nadie lo cerró a mano.
- El mismo evento movió la tarjeta de #12 a *Done* en el tablero, por el workflow
  *"Item closed → Status: Done"* del Project.
- Desde el issue #12 se llega al PR #15, del PR al commit `34ccaf1`, y del commit al archivo
  `.github/workflows/ci.yml`. Para el otro lado: el issue #12 muestra su *parent* #11, y #11 su
  *parent* #10.

La jerarquía usa **sub-issues** reales de GitHub, no listas de tareas en el texto. La diferencia
importa: las sub-issues dan la relación padre-hijo navegable en las dos direcciones y el
porcentaje de avance en el padre, mientras que una lista de casillas en el cuerpo es texto que hay
que mantener a mano y que no sabe si el issue que menciona está abierto o cerrado.

## 5. Problemas que encontré y cómo los resolví

### El campo Sprint no se puede crear desde la línea de comandos

Armé toda la estructura del TP3 con `gh` —etiquetas, épica, historia, tareas, bug, las relaciones
padre-hijo y el alta en el Project— y me faltaba el campo de sprint. `gh project field-create`
sólo acepta `--data-type {TEXT|SINGLE_SELECT|DATE|NUMBER}`: **el tipo `ITERATION` no está**, ni por
CLI ni por la API pública de Projects. Un campo de texto llamado "Sprint" no es lo mismo: el tipo
*Iteration* es el que tiene fechas de inicio y duración y el que permite después filtrar por
sprint actual.

Solución: ese campo lo creé desde la web, con duración de 1 semana. Lo dejo anotado porque es un
límite real de la herramienta y no algo que hice mal.

### Al probar el límite de WIP me quedó el tablero desordenado

Para comprobar si el límite de 2 bloqueaba o sólo avisaba, arrastré varias tarjetas a *In
Progress*. Comprobé lo que quería —avisa, no bloquea— pero quedaron en esa columna la épica y el
bug, que no estaban en curso. Un tablero que no refleja la realidad es peor que no tener tablero,
porque se lo lee y se saca una conclusión falsa.

Las devolví a *Todo* con `gh project item-edit`, pasando el id del campo Status y el id de la
opción, que se sacan de `gh project field-list --format json`.

### Elegir dónde colgaba el bug

Mi primer impulso fue colgar el bug de la historia #11, porque ahí estaba todo lo demás. Está mal:
#11 es sobre el pipeline de CI y el bug es de la pantalla de Stock, entregada en el TP2. Colgarlo
ahí habría hecho que la historia no se pudiera cerrar hasta arreglar algo que no tiene nada que
ver con ella. Un defecto sobre algo ya entregado no es parte de un incremento nuevo: va al
backlog, priorizado por su cuenta.

## 6. Declaración de uso de IA

Usé un asistente de IA para armar la estructura del TP3 —redactar los cuerpos de la épica, la
historia, las tareas y el bug, y automatizar su creación con `gh`— igual que en los prácticos
anteriores. Lo que decidí yo:

1. **Qué historia elegir.** La de CI no es un ejemplo inventado: es lo que sigue de verdad en el
   TP4, y por eso la tarea #12 pudo cerrarse con un PR real en lugar de simulado.
2. **La duración del sprint y el límite de WIP**, con las razones que están arriba. Ninguno de los
   dos números es un valor por defecto copiado.
3. **Verifiqué la trazabilidad en vez de suponerla**: comparé el instante del merge del PR con el
   del cierre del issue para confirmar que lo cerró la automatización y no yo.
4. **Probé el límite de WIP rompiéndolo**, para saber si avisa o bloquea. La respuesta cambia lo
   que significa el límite, y no quería escribirla de memoria.
5. **Corregí el criterio de dónde va el bug** cuando me di cuenta de que colgarlo de la historia la
   dejaba imposible de cerrar.

**Qué significa esto**: puedo defender cada número y cada relación del tablero. Lo que no puedo
decir es que redacté los textos de los issues a mano.
