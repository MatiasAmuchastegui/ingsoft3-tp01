/**
 * Lo que comparten las dos suites.
 */

/**
 * Las credenciales del entorno contra el que se prueba. Llegan por variables de entorno y
 * no están escritas en ningún archivo del repositorio.
 *
 * Si faltan, la suite falla al cargar, a la vista. Una prueba que se saltea sola cuando algo
 * no está es una prueba que nunca avisa.
 */
export function credenciales() {
  const email = process.env.E2E_EMAIL
  const password = process.env.E2E_PASSWORD
  if (!email || !password) {
    throw new Error('Faltan E2E_EMAIL y E2E_PASSWORD: las credenciales del entorno contra el que se prueba.')
  }
  return { email, password }
}

/**
 * Un prefijo de SKU que no existe todavía: 6 letras o números, con la hora adentro.
 *
 * El prefijo tiene que ser único entre las categorías, así que no se puede repetir uno fijo:
 * la segunda corrida chocaría con lo que dejó la primera si alguna falló a mitad de camino.
 */
export function prefijoUnico() {
  return ('T' + Date.now().toString(36).slice(-5)).toUpperCase()
}
