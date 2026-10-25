import * as Sentry from '@sentry/react'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import { AuthProvider } from './auth/AuthContext'
import './estilos.css'

// Sentry (TP9): el front cuenta qué falla en el navegador y cuánto tardan las pantallas y sus
// pedidos a la api.
//
// El DSN va escrito acá, y no es un descuido: el de un front no es un secreto. Sólo sirve para
// MANDAR datos a Sentry, no para leerlos, y de todos modos viajaría en el JavaScript que baja el
// navegador. Es la diferencia con una clave de API, que permite leer o modificar.
//
// La misma imagen corre en QA y en producción, así que el entorno sale del sitio desde el que se
// abrió la app: en Sentry queda separado lo de un entorno y lo del otro.
Sentry.init({
  dsn: 'https://d16e32934a8c14543fdb601625bd24d2@o4512302270906368.ingest.us.sentry.io/4512302334607360',
  environment: window.location.hostname,
  integrations: [Sentry.browserTracingIntegration()],
  tracesSampleRate: 1.0,
})

// Despierta la api desde el navegador apenas se abre la app (ver /despertar en la plantilla de
// nginx): un pedido que sale del front no la despierta. `no-cors` porque la respuesta no se lee,
// sólo importa que el pedido llegue. Si falla, no pasa nada: es sólo un empujón.
fetch('/despertar', { mode: 'no-cors' }).catch(() => {})

const contenedor = document.getElementById('root')
if (!contenedor) throw new Error('No se encontró el elemento #root en index.html.')

createRoot(contenedor).render(
  <StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <App />
      </AuthProvider>
    </BrowserRouter>
  </StrictMode>,
)
