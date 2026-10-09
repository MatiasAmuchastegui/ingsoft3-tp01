#!/usr/bin/env bash
#
# Prueba de humo: lo mínimo que tiene que contestar un entorno para considerarlo
# desplegado. No reemplaza a los tests del CI — ésos ya corrieron sobre el código y
# probaron la lógica. Esto prueba otra cosa: que lo que quedó arriba, arriba y conectado,
# responde. Un deploy puede salir "exitoso" y dejar la app sin base, sin configuración o
# sirviendo la página en blanco.
#
# Uso:  smoke.sh <url-del-front> <url-de-la-api>
set -euo pipefail

FRONT=${1:?falta la url del front}
API=${2:?falta la url de la api}

INTENTOS=30
ESPERA=20

# Render tarda en construir y, mientras tanto, la versión vieja sigue contestando. Esta
# pausa inicial evita el falso positivo más obvio: dar por bueno el deploy porque
# respondió el contenedor anterior, que todavía no se bajó.
echo "Dando tiempo a que Render levante la versión nueva..."
sleep 45

# $1 nombre para el log, $2 url, $3 metodo, $4 cuerpo (opcional)
esperar() {
  local nombre=$1 url=$2 metodo=${3:-GET} cuerpo=${4:-}
  local i codigo

  for i in $(seq 1 $INTENTOS); do
    if [ -n "$cuerpo" ]; then
      codigo=$(curl -s -o /tmp/respuesta -w '%{http_code}' --max-time 30 \
        -X "$metodo" "$url" -H 'Content-Type: application/json' -d "$cuerpo" || echo 000)
    else
      codigo=$(curl -s -o /tmp/respuesta -w '%{http_code}' --max-time 30 "$url" || echo 000)
    fi

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

# 3. El camino de verdad, el que usa el navegador: entra por nginx, sale por el proxy,
#    llega al backend y consulta la base. Es el único endpoint que toca datos sin pedir
#    un token antes, así que es el que se puede probar desde afuera.
esperar "login a traves del front" "$FRONT/api/auth/login" POST \
  '{"email":"admin@joyeria.local","password":"Admin123!"}'

echo
echo "Humo limpio."
