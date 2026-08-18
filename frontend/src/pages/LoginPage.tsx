import { useState } from 'react'
import type { FormEvent } from 'react'
import { useAuth } from '../auth/AuthContext'

export default function LoginPage() {
  const { login } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  // Comportamiento testeable en el TP5: el botón no se habilita con datos incompletos.
  const formularioValido = email.trim().length > 0 && password.length > 0

  async function manejarSubmit(evento: FormEvent) {
    evento.preventDefault()
    if (!formularioValido || enviando) return

    setEnviando(true)
    setError(null)
    try {
      await login(email.trim(), password)
    } catch (e) {
      setError(e instanceof Error ? e.message : 'No se pudo iniciar sesión.')
    } finally {
      setEnviando(false)
    }
  }

  return (
    <div className="pantalla-centrada">
      <form className="tarjeta login" onSubmit={manejarSubmit}>
        <h1>Joyería · Stock</h1>
        <p className="ayuda">Ingresá con tu usuario para ver el stock de tu local.</p>

        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          autoComplete="username"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="admin@joyeria.local"
        />

        <label htmlFor="password">Contraseña</label>
        <input
          id="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />

        {error && (
          <p className="mensaje-error" role="alert">
            {error}
          </p>
        )}

        <button type="submit" disabled={!formularioValido || enviando}>
          {enviando ? 'Ingresando…' : 'Ingresar'}
        </button>

        <details className="ayuda-credenciales">
          <summary>Usuarios de prueba</summary>
          <ul>
            <li>
              <code>admin@joyeria.local</code> / <code>Admin123!</code> — ve los 3 locales
            </li>
            <li>
              <code>vendedor1@joyeria.local</code> / <code>Vendedor123!</code> — sólo Sucursal Centro
            </li>
          </ul>
        </details>
      </form>
    </div>
  )
}
