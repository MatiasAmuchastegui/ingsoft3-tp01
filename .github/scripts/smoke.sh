#!/usr/bin/env bash
#
# Prueba de humo: lo mínimo que tiene que contestar un entorno para considerarlo
# desplegado. No reemplaza a los tests del CI — ésos ya corrieron sobre el código y
# probaron la lógica. Esto prueba otra cosa: que lo que quedó arriba, arriba y conectado,
# responde. Un deploy puede salir "exitoso" y dejar la app sin base, sin configuración o
# sirviendo la página en blanco.
#
# Prueba que el entorno RESPONDE, no que FUNCIONA: eso lo prueban la integración y las e2e.
# Por eso acá no hay ningún login, y el script no lleva credenciales.
#
# Uso:  smoke.sh <url-del-front> <url-de-la-api>
set -euo pipefail

FRONT=${1:?falta la url del front}
API=${2:?falta la url de la api}

INTENTOS=30
ESPERA=20

# Render tarda en bajar la imagen y arrancarla y, mientras tanto, la versión vieja sigue
# contestando. Esta pausa inicial evita el falso positivo más obvio: dar por bueno el
# deploy porque respondió el contenedor anterior, que todavía no se bajó.
echo "Dando tiempo a que Render levante la versión nueva..."
sleep 45

# $1 nombre para el log, $2 url
esperar() {
  local nombre=$1 url=$2
  local i codigo

  for i in $(seq 1 $INTENTOS); do
    # Si no hay conexión, -w igual escribe 000; el `|| codigo=000` sólo evita que el
    # `set -e` corte el script en el primer intento fallido.
    codigo=$(curl -s -o /tmp/respuesta -w '%{http_code}' --max-time 30 "$url") || codigo=000

    if [ "$codigo" = "200" ]; then
      echo "  OK  $nombre (intento $i)"
      return 0
    fi
    echo "  ..  $nombre devolvió $codigo (intento $i de $INTENTOS)"
    sleep $ESPERA
  done

  echo "::error::$nombre nunca respondió 200. Última respuesta:"
  head -c 400 /tmp/respuesta || true
  return 1
}

echo "Front: $FRONT"
echo "API:   $API"
echo

# 1. nginx levantó. No toca el backend, así que aísla el problema: si esto falla, el
#    roto es el front; si esto anda y lo de abajo no, el roto es el backend.
esperar "el front sirve la pagina"   "$FRONT/"
esperar "nginx esta vivo"            "$FRONT/nginx-health"

# 2. El backend está vivo Y su base responde: el healthcheck incluye AddDbContextCheck,
#    así que un 200 acá descarta también que la cadena de conexión esté mal.
esperar "la api responde y ve la base" "$API/health"

# 3. El camino que usa el navegador: entra por nginx, el proxy lo manda al backend y el
#    healthcheck consulta la base. Si BACKEND_URL estuviera mal cargada en el front, esto
#    falla aunque lo de arriba ande.
esperar "la api responde a traves del front" "$FRONT/health"

echo
echo "Humo limpio."
