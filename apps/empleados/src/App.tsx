import { useCallback, useEffect, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { ApiError, api } from './api'
import type { Alfajor, Client, Employee, Invoice, Lookup, Operation, Payment, Presentation, Session, SessionUser, StockItem } from './api'
import './App.css'

type View = 'dashboard' | 'alfajores' | 'tipos' | 'sabores' | 'coberturas' | 'presentaciones' | 'stock' | 'clientes' | 'nueva-venta' | 'operaciones' | 'pagos' | 'facturas' | 'promociones' | 'empleados'
type Role = SessionUser['role']
type PriceCombination = { alfajorId: number; presentationId: number; presentation: string; units: number; price: number; stock: number; stockMinimum: number }
const money = (value: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)
const rolesTitle: Record<Role, string> = { ADMINISTRATIVO: 'Administrativo', VENDEDOR: 'Vendedor', CLIENTE: 'Cliente' }
const menu: { id: View; title: string; icon: string; roles: Role[] }[] = [
  { id: 'dashboard', title: 'Resumen', icon: '⌂', roles: ['ADMINISTRATIVO', 'VENDEDOR'] },
  { id: 'alfajores', title: 'Alfajores', icon: '◉', roles: ['ADMINISTRATIVO', 'VENDEDOR'] },
  { id: 'tipos', title: 'Tipos', icon: '◇', roles: ['ADMINISTRATIVO'] },
  { id: 'sabores', title: 'Sabores', icon: '◌', roles: ['ADMINISTRATIVO'] },
  { id: 'coberturas', title: 'Coberturas', icon: '◈', roles: ['ADMINISTRATIVO'] },
  { id: 'presentaciones', title: 'Presentaciones', icon: '▤', roles: ['ADMINISTRATIVO'] },
  { id: 'stock', title: 'Stock', icon: '▦', roles: ['ADMINISTRATIVO', 'VENDEDOR'] },
  { id: 'clientes', title: 'Clientes', icon: '♡', roles: ['ADMINISTRATIVO', 'VENDEDOR'] },
  { id: 'nueva-venta', title: 'Nueva venta', icon: '＋', roles: ['VENDEDOR'] },
  { id: 'operaciones', title: 'Operaciones', icon: '↗', roles: ['ADMINISTRATIVO', 'VENDEDOR'] },
  { id: 'pagos', title: 'Pagos', icon: '$', roles: ['ADMINISTRATIVO'] },
  { id: 'facturas', title: 'Facturas', icon: '▧', roles: ['ADMINISTRATIVO', 'VENDEDOR'] },
  { id: 'promociones', title: 'Promociones', icon: '%', roles: ['ADMINISTRATIVO'] },
  { id: 'empleados', title: 'Empleados', icon: '♙', roles: ['ADMINISTRATIVO'] },
]
const lookupRoute: Record<'tipos' | 'sabores' | 'coberturas', string> = { tipos: 'tipos', sabores: 'sabores', coberturas: 'coberturas' }

function storedSession(): Session | null {
  try { return JSON.parse(localStorage.getItem('alfajorge-employee-session') || 'null') as Session | null }
  catch { return null }
}

function App() {
  const [session, setSession] = useState<Session | null>(storedSession)
  const [view, setView] = useState<View>('dashboard')
  const [data, setData] = useState<unknown[]>([])
  const [saleAlfajores, setSaleAlfajores] = useState<Alfajor[]>([])
  const [lookups, setLookups] = useState<{ tipos: Lookup[]; sabores: Lookup[]; coberturas: Lookup[]; presentaciones: Presentation[] }>({ tipos: [], sabores: [], coberturas: [], presentaciones: [] })
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [apiOnline, setApiOnline] = useState<boolean | null>(null)
  const [setupRequired, setSetupRequired] = useState(false)
  const [filter, setFilter] = useState('')
  const user = session?.user
  const availableMenu = menu.filter(item => user && item.roles.includes(user.role))
  const title = menu.find(item => item.id === view)?.title || 'Resumen'

  useEffect(() => {
    let cancelled = false
    void api<{ status: string }>('/api/health')
      .then(() => { if (!cancelled) setApiOnline(true) })
      .catch(() => { if (!cancelled) setApiOnline(false) })
    void api<{ setupRequired: boolean }>('/api/auth/setup-status')
      .then(result => { if (!cancelled) setSetupRequired(result.setupRequired) })
      .catch(error => { if (!cancelled) setMessage(error instanceof Error ? error.message : 'No se pudo verificar la configuración inicial.') })
    return () => { cancelled = true }
  }, [])

  useEffect(() => {
    const token = session?.token
    if (!token) return
    let cancelled = false
    void api<SessionUser>('/api/auth/me', token).then(authenticatedUser => {
      if (!['ADMINISTRATIVO', 'VENDEDOR'].includes(authenticatedUser.role)) throw new Error('Esta cuenta no tiene acceso al portal interno.')
      if (!cancelled) setSession(current => current ? { ...current, user: authenticatedUser } : current)
    }).catch(error => {
      if (cancelled) return
      const authorizationFailure = error instanceof ApiError && (error.status === 401 || error.status === 403)
        || error instanceof Error && error.message === 'Esta cuenta no tiene acceso al portal interno.'
      if (authorizationFailure) {
        localStorage.removeItem('alfajorge-employee-session')
        setSession(null)
        setData([])
        setMessage('La sesión expiró o ya no tiene permisos. Ingresá nuevamente.')
      } else {
        setMessage(error instanceof Error ? error.message : 'No se pudo validar la sesión con el servidor.')
      }
    })
    return () => { cancelled = true }
  }, [session?.token])

  const loadView = useCallback(async (target: View) => {
    if (!session) return
    setMessage('')
    setData([])
    try {
      if (target === 'dashboard') {
        const operationPath = user?.role === 'ADMINISTRATIVO' ? '/api/operaciones' : '/api/operaciones/mias'
        const [operations, stock] = await Promise.all([api<Operation[]>(operationPath, session.token), api<StockItem[]>(user?.role === 'ADMINISTRATIVO' ? '/api/stock' : '/api/stock/disponible', session.token)])
        setData([...operations.map(item => ({ ...item, kind: 'operacion' })), ...stock.map(item => ({ ...item, kind: 'stock' }))])
        return
      }
      if (target === 'nueva-venta') {
        const [clients, alfajores] = await Promise.all([
          api<Client[]>('/api/clientes', session.token),
          api<Alfajor[]>('/api/alfajores', session.token),
        ])
        setData(clients)
        setSaleAlfajores(alfajores)
        return
      }
      const endpoints: Record<View, string> = {
        dashboard: '/api/operaciones', alfajores: '/api/alfajores', tipos: '/api/tipos', sabores: '/api/sabores', coberturas: '/api/coberturas', presentaciones: '/api/presentaciones', stock: user?.role === 'ADMINISTRATIVO' ? '/api/stock' : '/api/stock/disponible', clientes: '/api/clientes', 'nueva-venta': '/api/alfajores', operaciones: user?.role === 'ADMINISTRATIVO' ? '/api/operaciones' : '/api/operaciones/mias', pagos: '/api/pagos', facturas: user?.role === 'ADMINISTRATIVO' ? '/api/facturas' : '/api/facturas/mias', promociones: '/api/promociones/activas', empleados: '/api/empleados',
      }
      const records = await api<unknown[]>(endpoints[target], session.token)
      setData(records)
      if (['alfajores', 'nueva-venta'].includes(target) && user?.role === 'ADMINISTRATIVO') {
        const [tipos, sabores, coberturas, presentaciones] = await Promise.all([
          api<Lookup[]>('/api/tipos', session.token), api<Lookup[]>('/api/sabores', session.token), api<Lookup[]>('/api/coberturas', session.token), api<Presentation[]>('/api/presentaciones', session.token),
        ])
        setLookups({ tipos, sabores, coberturas, presentaciones })
      }
    } catch (error) { setData([]); setMessage(error instanceof Error ? error.message : 'No se pudieron cargar los datos.') }
  }, [session, user?.role])

  useEffect(() => { if (session) void loadView(view) }, [session?.token, view, loadView])

  async function signIn(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setMessage('')
    const values = Object.fromEntries(new FormData(event.currentTarget).entries())
    try {
      const result = await api<Session>('/api/auth/login', undefined, { method: 'POST', body: values })
      if (!['ADMINISTRATIVO', 'VENDEDOR'].includes(result.user.role)) { setMessage('Esta cuenta es de cliente. Usá el portal comercial.'); return }
      localStorage.setItem('alfajorge-employee-session', JSON.stringify(result)); setSession(result)
    } catch (error) { setMessage(error instanceof Error ? error.message : 'No se pudo iniciar sesión.') }
    finally { setBusy(false) }
  }

  async function setupAdministrator(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setMessage('')
    const values = Object.fromEntries(new FormData(event.currentTarget).entries())
    try {
      const result = await api<Session>('/api/auth/setup-admin', undefined, { method: 'POST', body: values })
      localStorage.setItem('alfajorge-employee-session', JSON.stringify(result))
      setSetupRequired(false)
      setSession(result)
    } catch (error) { setMessage(error instanceof Error ? error.message : 'No se pudo crear la cuenta administradora.') }
    finally { setBusy(false) }
  }

  async function signOut() {
    localStorage.removeItem('alfajorge-employee-session'); setSession(null); setData([]); setView('dashboard')
  }

  if (!session || !user) return <LoginView busy={busy} message={message} setupRequired={setupRequired} onSubmit={signIn} onSetup={setupAdministrator} />

  return <div className="admin-shell">
    <aside className="admin-sidebar"><a className="admin-brand" href="/"><img src="/assets/logo-alfajorge.jpeg" alt="Alfajorge" /><div><strong>Alfajorge</strong><small>Panel interno</small></div></a><p className="sidebar-kicker">FÁBRICA · {rolesTitle[user.role].toUpperCase()}</p><nav>{availableMenu.map(item => <button key={item.id} className={view === item.id ? 'selected' : ''} onClick={() => { setView(item.id); setFilter('') }}><span>{item.icon}</span>{item.title}</button>)}</nav><div className="admin-user"><span className="user-avatar">{user.name.slice(0, 1)}{user.lastName.slice(0, 1)}</span><div><strong>{user.name} {user.lastName}</strong><small>{rolesTitle[user.role]}</small></div><button onClick={signOut} aria-label="Cerrar sesión">↪</button></div></aside>
    <main className="admin-main"><header className="admin-topbar"><div><span className="breadcrumb">Operaciones /</span><strong>{title}</strong></div><div><span className={`connection-dot ${apiOnline === false ? 'offline' : ''}`} />{apiOnline === null ? 'Verificando API…' : apiOnline ? 'API y base local conectadas' : 'API/base local no disponible'}</div></header><section className="admin-content"><div className="page-title"><div><span className="eyebrow">{rolesTitle[user.role].toUpperCase()}</span><h1>{title}</h1><p>{descriptionFor(view)}</p></div>{view === 'alfajores' && user.role === 'ADMINISTRATIVO' && <button className="button button-primary" onClick={() => document.querySelector('#alfajor-form')?.scrollIntoView({ behavior: 'smooth' })}>＋ Nuevo alfajor</button>}</div>{message && <div className="notice" role="status">{message}</div>}{renderPage()}</section></main>
  </div>

  function renderPage(): ReactNode {
    if (!session || !user) return null
    if (view === 'dashboard') return <Dashboard data={data} role={user.role} />
    if (view === 'nueva-venta') return <SaleForm clients={data as Client[]} alfajores={saleAlfajores} onSave={saveSale} />
    if (view === 'alfajores' && user.role === 'ADMINISTRATIVO') return <ProductsAdmin products={data as Alfajor[]} lookups={lookups} token={session.token} onSave={saveProduct} onDelete={deleteProduct} onRefresh={() => void loadView('alfajores')} />
    if (view === 'tipos' || view === 'sabores' || view === 'coberturas' || view === 'presentaciones') return <LookupAdmin name={view} records={data as Lookup[]} token={session.token} onRefresh={() => void loadView(view)} />
    if (view === 'stock') return <DataTable view={view} records={data as StockItem[]} filter={filter} onFilter={setFilter} />
    if (view === 'empleados') return <EmployeesTable records={data as Employee[]} />
    if (view === 'clientes') return <DataTable view={view} records={data as Client[]} filter={filter} onFilter={setFilter} />
    if (view === 'operaciones') return <DataTable view={view} records={data as Operation[]} filter={filter} onFilter={setFilter} onChangeOperationStatus={user.role === 'ADMINISTRATIVO' ? changeOperationStatus : undefined} onCancelOperation={cancelOperation} busy={busy} />
    if (view === 'pagos') return <DataTable view={view} records={data as Payment[]} filter={filter} onFilter={setFilter} onConfirmPayment={confirmPayment} busy={busy} />
    if (view === 'facturas') return <DataTable view={view} records={data as Invoice[]} filter={filter} onFilter={setFilter} />
    if (view === 'promociones') return <DataTable view={view} records={data as Record<string, unknown>[]} filter={filter} onFilter={setFilter} />
    return <DataTable view={view} records={data as Alfajor[]} filter={filter} onFilter={setFilter} />
  }

  async function saveProduct(values: Record<string, unknown>): Promise<boolean> {
    if (!session) return false
    try {
      await api('/api/alfajores', session.token, { method: 'POST', body: values })
      await loadView('alfajores')
      setMessage('Alfajor creado.')
      return true
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No se pudo crear el alfajor.')
      return false
    }
  }

  async function deleteProduct(productId: number): Promise<boolean> {
    if (!session) return false
    try {
      await api(`/api/alfajores/${productId}`, session.token, { method: 'DELETE' })
      await loadView('alfajores')
      setMessage('Alfajor eliminado del catálogo.')
      return true
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No se pudo eliminar el alfajor.')
      return false
    }
  }

  async function saveSale(values: { clientId: number; alfajorId: number; presentationId: number; quantity: number; paymentMethod: string }) {
    if (!session) return
    try {
      await api(`/api/operaciones/presencial?idCliente=${values.clientId}`, session.token, {
        method: 'POST',
        body: { items: [{ alfajorId: values.alfajorId, presentationId: values.presentationId, quantity: values.quantity }], paymentMethod: values.paymentMethod },
      })
      setView('operaciones'); setMessage('Venta registrada y stock actualizado.')
    } catch (error) { setMessage(error instanceof Error ? error.message : 'No se pudo registrar la venta.') }
  }

  async function confirmPayment(paymentId: number) {
    if (!session || user?.role !== 'ADMINISTRATIVO') return
    setBusy(true)
    try {
      await api(`/api/pagos/${paymentId}/confirmar`, session.token, { method: 'POST' })
      await loadView('pagos')
      setMessage(`Pago #${paymentId} confirmado; operación, stock y factura actualizados.`)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No se pudo confirmar el pago.')
    } finally {
      setBusy(false)
    }

  }

  async function changeOperationStatus(operationId: number, status: string) {
    if (!session || user?.role !== 'ADMINISTRATIVO') return
    setBusy(true)
    try {
      await api(`/api/operaciones/${operationId}/estado`, session.token, {
        method: 'PUT',
        body: { status },
      })
      await loadView('operaciones')
      setMessage(`Operación #${operationId} actualizada a ${status}.`)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No se pudo cambiar el estado de la operación.')
    } finally {
      setBusy(false)
    }
  }

  async function cancelOperation(operationId: number) {
    if (!session) return
    setBusy(true)
    try {
      await api(`/api/operaciones/${operationId}/cancelar`, session.token, { method: 'POST' })
      await loadView('operaciones')
      setMessage(`Operación #${operationId} cancelada.`)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No se pudo cancelar la operación.')
    } finally {
      setBusy(false)
    }
  }
}

function descriptionFor(view: View) {
  const descriptions: Record<View, string> = { dashboard: 'Resumen de ventas y disponibilidad.', alfajores: 'Catálogo de productos, sabores, coberturas y presentaciones.', tipos: 'Clasificación de los alfajores.', sabores: 'Sabores disponibles para el catálogo.', coberturas: 'Coberturas disponibles para el catálogo.', presentaciones: 'Formatos de venta y unidades por presentación.', stock: 'Existencias y mínimos por alfajor y presentación.', clientes: 'Personas registradas como clientes.', 'nueva-venta': 'Venta presencial asociada al vendedor autenticado.', operaciones: 'Operaciones registradas y sus estados.', pagos: 'Pagos registrados en la base.', facturas: 'Facturas emitidas luego de aprobar un pago.', promociones: 'Promociones vigentes.', empleados: 'Vendedores y administrativos autorizados.' }
  return descriptions[view]
}

function LoginView({ busy, message, setupRequired, onSubmit, onSetup }: { busy: boolean; message: string; setupRequired: boolean; onSubmit: (event: FormEvent<HTMLFormElement>) => void; onSetup: (event: FormEvent<HTMLFormElement>) => void }) {
  const [showSetup, setShowSetup] = useState(false)
  return <main className="employee-login"><section className="login-brand"><a className="admin-brand" href="/"><img src="/assets/logo-alfajorge.jpeg" alt="Alfajorge" /><div><strong>Alfajorge</strong><small>Panel interno</small></div></a><div className="brand-badge" aria-label="Logo de Alfajorge"><img src="/assets/logo-alfajorge.jpeg" alt="Logo de Alfajorge" /></div><div><span className="eyebrow">SISTEMA INTERNO</span><h1>El trabajo detrás<br />de cada alfajor.</h1><p>Acceso exclusivo para personal autorizado.</p></div></section><section className="login-panel">
    <span className="eyebrow">PERSONAL ALFAJORGE</span>
    {showSetup && setupRequired ? <>
      <h2>Crear administrador</h2>
      <p>Configuración inicial para esta máquina. Solo se puede crear una cuenta.</p>
      <form onSubmit={onSetup}>
        <label>DNI<input name="dni" required maxLength={20} autoComplete="off" /></label>
        <label>Nombre<input name="name" required maxLength={60} autoComplete="given-name" /></label>
        <label>Apellido<input name="lastName" required maxLength={60} autoComplete="family-name" /></label>
        <label>Correo electrónico<input name="email" type="email" required maxLength={150} autoComplete="email" /></label>
        <label>Contraseña (mínimo 10 caracteres)<input name="password" type="password" required minLength={10} autoComplete="new-password" /></label>
        <button className="button button-primary" disabled={busy}>{busy ? 'Creando cuenta…' : 'Crear administrador'}</button>
        {message && <p className="form-error" role="alert">{message}</p>}
      </form>
      <button className="login-switch" type="button" onClick={() => setShowSetup(false)}>Volver al inicio de sesión</button>
    </> : <>
      <h2>Ingresar</h2>
      <p>Usá las credenciales asignadas por administración.</p>
      <form onSubmit={onSubmit}>
        <label>Correo electrónico<input name="email" type="email" required autoComplete="username" /></label>
        <label>Contraseña<input name="password" type="password" required autoComplete="current-password" /></label>
        <button className="button button-primary" disabled={busy}>{busy ? 'Ingresando…' : 'Entrar al panel'}</button>
        {message && <p className="form-error" role="alert">{message}</p>}
      </form>
      {setupRequired && <div className="first-admin"><p>Todavía no hay cuentas de personal. Creá el primer administrador para configurar el sistema.</p><button className="login-switch" type="button" onClick={() => { setShowSetup(true) }}>Configurar primer administrador</button></div>}
    </>}
    <a className="public-link" href={import.meta.env.VITE_CLIENTS_URL || 'http://localhost:5173'}>Ir al portal de clientes ↗</a>
  </section></main>
}

function Dashboard({ data, role }: { data: unknown[]; role: Role }) {
  const operations = data.filter(item => 'origin' in (item as object)) as Operation[]
  const stock = data.filter(item => 'quantity' in (item as object) && 'alfajor' in (item as object)) as StockItem[]
  const total = operations.reduce((sum, item) => sum + item.total, 0)
  return <div className="stats-grid"><Stat label="Operaciones" value={String(operations.length)} note={role === 'VENDEDOR' ? 'Tus ventas registradas' : 'Todas las operaciones'} icon="↗" /><Stat label="Total operaciones" value={money(total)} note="Importes guardados en la base local" icon="$" /><Stat label="Presentaciones con stock" value={String(stock.length)} note="Existencias consultadas en vivo" icon="▦" /></div>
}
function Stat({ label, value, note, icon }: { label: string; value: string; note: string; icon: string }) { return <article className="stat-card"><div><span>{label}</span><i>{icon}</i></div><strong>{value}</strong><small>{note}</small></article> }

function DataTable({ view, records, filter, onFilter, onConfirmPayment, onChangeOperationStatus, onCancelOperation, onDeleteAlfajor, busy = false, role }: { view: View; records: Record<string, unknown>[]; filter: string; onFilter: (value: string) => void; onConfirmPayment?: (paymentId: number) => void; onChangeOperationStatus?: (operationId: number, status: string) => void; onCancelOperation?: (operationId: number) => void; onDeleteAlfajor?: (alfajorId: number) => void; busy?: boolean; role?: Role }) {
  void role
  const [confirmCancelId, setConfirmCancelId] = useState<number | null>(null)
  const [confirmDeleteId, setConfirmDeleteId] = useState<number | null>(null)
  const filtered = records.filter(record => Object.values(record).some(value => String(value ?? '').toLowerCase().includes(filter.toLowerCase())))
  const columns: Record<View, [string,string][]> = {
    dashboard: [], alfajores: [['name','Alfajor'],['type','Tipo'],['flavor','Sabor'],['coverage','Cobertura']], tipos: [['id','ID'],['name','Nombre'],['description','Descripción']], sabores: [['id','ID'],['name','Nombre'],['description','Descripción']], coberturas: [['id','ID'],['name','Nombre'],['description','Descripción']], presentaciones: [['id','ID'],['name','Nombre'],['units','Unidades']],
    stock: [['alfajor','Alfajor'],['presentation','Presentación'],['quantity','Unidades'],['minimum','Mínimo'],['updated','Actualizado']],
    clientes: [['dni','DNI'],['name','Nombre'],['lastName','Apellido'],['email','Correo'],['phone','Teléfono']],
    'nueva-venta': [], operaciones: [['id','Operación'],['date','Fecha'],['origin','Origen'],['status','Estado'],['total','Total'],['client','Cliente']],
    pagos: [['id','Pago'],['operationId','Operación'],['method','Método'],['amount','Monto'],['status','Estado'],['date','Fecha']],
    facturas: [['number','Factura'],['date','Fecha'],['total','Total']], promociones: [['name','Promoción'],['discount','Descuento'],['starts','Desde'],['ends','Hasta']], empleados: [['dni','DNI'],['name','Nombre'],['lastName','Apellido'],['email','Correo'],['cargo','Cargo'],['isSeller','Vendedor']],
  }
  const fields = columns[view]
  return <><div className="table-toolbar"><input placeholder="Buscar en los registros" value={filter} onChange={event => onFilter(event.target.value)} /><span>{filtered.length} registros</span></div><div className="table-panel"><div className="table-scroll"><table><thead><tr>{fields.map(([key,label]) => <th key={key}>{label}</th>)}{view === 'pagos' && onConfirmPayment && <th>Acción</th>}{view === 'operaciones' && (onChangeOperationStatus || onCancelOperation) && <th>Acción</th>}{view === 'alfajores' && onDeleteAlfajor && <th>Acción</th>}</tr></thead><tbody>{filtered.map((row,index) => <tr key={String(row.id ?? row.alfajorId ?? index)}>{fields.map(([key]) => <td key={key}>{key === 'total' || key === 'amount' ? money(Number(row[key])) : key === 'isSeller' ? (row[key] ? 'Sí' : 'No') : String(row[key] ?? '—')}</td>)}{view === 'pagos' && onConfirmPayment && <td>{row.status === 'PENDIENTE' ? <button className="button button-primary" disabled={busy} onClick={() => onConfirmPayment(Number(row.id))}>Confirmar</button> : '—'}</td>}{view === 'operaciones' && (onChangeOperationStatus || onCancelOperation) && <td>{nextOperationStatus(row.status) && onChangeOperationStatus ? <button className="button button-primary" disabled={busy} onClick={() => onChangeOperationStatus(Number(row.id), nextOperationStatus(row.status)!)}>{nextOperationStatus(row.status) === 'PREPARACION' ? 'Preparar' : nextOperationStatus(row.status) === 'ENVIADA' ? 'Marcar enviada' : 'Marcar entregada'}</button> : row.status === 'PENDIENTE' && onCancelOperation ? <button className="button button-danger" disabled={busy} onClick={() => { const id = Number(row.id); if (confirmCancelId === id) { onCancelOperation(id); setConfirmCancelId(null) } else setConfirmCancelId(id) }}>{confirmCancelId === Number(row.id) ? 'Confirmar cancelación' : 'Cancelar'}</button> : '—'}</td>}{view === 'alfajores' && onDeleteAlfajor && <td><button className="button button-danger" disabled={busy} onClick={() => { const id = Number(row.id); if (confirmDeleteId === id) { onDeleteAlfajor(id); setConfirmDeleteId(null) } else setConfirmDeleteId(id) }}>{confirmDeleteId === Number(row.id) ? 'Confirmar eliminación' : 'Eliminar'}</button></td>}</tr>)}</tbody></table>{filtered.length === 0 && <div className="table-empty">No hay registros para mostrar.</div>}</div></div></>
}

function nextOperationStatus(status: unknown): string | null {
  if (status === 'PAGADA') return 'PREPARACION'
  if (status === 'PREPARACION') return 'ENVIADA'
  if (status === 'ENVIADA') return 'ENTREGADA'
  return null
}

function EmployeesTable({ records }: { records: Employee[] }) {
  return <table className="data-table"><thead><tr><th>DNI</th><th>Nombre</th><th>Apellido</th><th>Correo</th><th>Cargo</th><th>Rol</th></tr></thead><tbody>{records.map(employee => (
    <tr key={employee.id}><td>{employee.dni}</td><td>{employee.name}</td><td>{employee.lastName}</td><td>{employee.email}</td><td>{employee.cargo}</td><td>{employee.isSeller ? 'Vendedor' : 'Administrativo'}</td></tr>
  ))}</tbody></table>
}

function ProductsAdmin({ products, lookups, token, onSave, onDelete, onRefresh }: { products: Alfajor[]; lookups: { tipos: Lookup[]; sabores: Lookup[]; coberturas: Lookup[]; presentaciones: Presentation[] }; token: string; onSave: (value: Record<string, unknown>) => Promise<boolean>; onDelete: (alfajorId: number) => Promise<boolean>; onRefresh: () => void }) {
  const [deleteBusy, setDeleteBusy] = useState(false)
  async function deleteAlfajor(alfajorId: number) {
    setDeleteBusy(true)
    try {
      await onDelete(alfajorId)
    } finally {
      setDeleteBusy(false)
    }
  }
  return <><form className="admin-form" id="alfajor-form" onSubmit={async event => { event.preventDefault(); const form = event.currentTarget; const values = new FormData(form); const request = { name: String(values.get('name')), description: String(values.get('description') || ''), imageUrl: String(values.get('imageUrl') || ''), typeId: Number(values.get('typeId')), flavorId: Number(values.get('flavorId')), coverageId: Number(values.get('coverageId')), presentations: [{ presentationId: Number(values.get('presentationId')), price: Number(values.get('price')), stock: Number(values.get('stock')), stockMinimum: Number(values.get('stockMinimum')) }] }; if (await onSave(request)) form.reset() }}><h2>Nuevo alfajor</h2><div className="form-grid"><label>Nombre<input name="name" required /></label><label>Imagen URL<input name="imageUrl" type="url" /></label><label className="wide">Descripción<input name="description" /></label><label>Tipo<select name="typeId" required><option value="">Elegí tipo</option>{lookups.tipos.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><label>Sabor<select name="flavorId" required><option value="">Elegí sabor</option>{lookups.sabores.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><label>Cobertura<select name="coverageId" required><option value="">Elegí cobertura</option>{lookups.coberturas.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><label>Presentación inicial<select name="presentationId" required><option value="">Elegí presentación</option>{lookups.presentaciones.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><label>Precio<input name="price" type="number" min="0" step="0.01" required /></label><label>Stock inicial<input name="stock" type="number" min="0" required /></label><label>Stock mínimo<input name="stockMinimum" type="number" min="0" required /></label></div><button className="button button-primary">Guardar alfajor</button></form><PriceManager products={products} presentations={lookups.presentaciones} token={token} onRefresh={onRefresh} /><DataTable view="alfajores" records={products as unknown as Record<string,unknown>[]} filter="" onFilter={() => undefined} onDeleteAlfajor={alfajorId => void deleteAlfajor(alfajorId)} busy={deleteBusy} role="ADMINISTRATIVO" /></>
}

function PriceManager({ products, presentations, token, onRefresh }: { products: Alfajor[]; presentations: Presentation[]; token: string; onRefresh: () => void }) {
  const [combinations, setCombinations] = useState<Record<number, PriceCombination[]>>({})
  const [selectedAlfajorId, setSelectedAlfajorId] = useState<number>(products[0]?.id || 0)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    let cancelled = false
    void Promise.all(products.map(async product => [
      product.id,
      await api<PriceCombination[]>(`/api/alfajores/${product.id}/presentaciones`, token),
    ] as const)).then(results => {
      if (!cancelled) setCombinations(Object.fromEntries(results))
    }).catch(reason => {
      if (!cancelled) setError(reason instanceof Error ? reason.message : 'No se pudieron cargar los precios.')
    })
    return () => { cancelled = true }
  }, [products, token])

  const currentCombinations = combinations[selectedAlfajorId] || []
  const unusedPresentations = presentations.filter(presentation =>
    !currentCombinations.some(combination => combination.presentationId === presentation.id))

  async function updatePrice(event: FormEvent<HTMLFormElement>, presentationId: number) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    const price = Number(form.get('price'))
    setBusy(true); setError(''); setNotice('')
    try {
      await api(`/api/alfajores/${selectedAlfajorId}/presentaciones/${presentationId}/precio`, token, {
        method: 'PUT',
        body: { price },
      })
      setNotice('Precio actualizado.')
      onRefresh()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No se pudo actualizar el precio.')
    } finally {
      setBusy(false)
    }
  }

  async function addCombination(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = new FormData(event.currentTarget)
    setBusy(true); setError(''); setNotice('')
    try {
      await api(`/api/alfajores/${selectedAlfajorId}/presentaciones`, token, {
        method: 'POST',
        body: {
          presentationId: Number(form.get('presentationId')),
          price: Number(form.get('price')),
          stock: Number(form.get('stock')),
          stockMinimum: Number(form.get('stockMinimum')),
        },
      })
      setNotice('Presentación y precio agregados al alfajor.')
      onRefresh()
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'No se pudo agregar la presentación.')
    } finally {
      setBusy(false)
    }
  }

  return <section className="admin-form price-manager">
    <h2>Precios por presentación</h2>
    {!products.length ? <p>No hay alfajores activos para administrar.</p> : <>
      <label>Alfajor<select value={selectedAlfajorId} onChange={event => setSelectedAlfajorId(Number(event.target.value))}>{products.map(product => <option key={product.id} value={product.id}>{product.name}</option>)}</select></label>
      {error && <p className="form-error" role="alert">{error}</p>}
      {notice && <p className="form-help" role="status">{notice}</p>}
      <div className="price-list">{currentCombinations.map(combination => <form className="price-row" key={combination.presentationId} onSubmit={event => void updatePrice(event, combination.presentationId)}>
        <div><strong>{combination.presentation}</strong><span>{combination.units} unidades · Stock: {combination.stock}</span></div>
        <label>Precio<input name="price" type="number" min="0" max="9999999999.99" step="0.01" defaultValue={combination.price} required /></label>
        <button className="button button-primary" disabled={busy}>Guardar precio</button>
      </form>)}</div>
      {unusedPresentations.length > 0 && <form className="price-add-form" onSubmit={event => void addCombination(event)}>
        <h3>Agregar presentación</h3>
        <div className="form-grid">
          <label>Presentación<select name="presentationId" required><option value="">Elegí presentación</option>{unusedPresentations.map(item => <option key={item.id} value={item.id}>{item.name} · {item.units} u</option>)}</select></label>
          <label>Precio<input name="price" type="number" min="0" max="9999999999.99" step="0.01" required /></label>
          <label>Stock inicial<input name="stock" type="number" min="0" step="1" defaultValue="0" required /></label>
          <label>Stock mínimo<input name="stockMinimum" type="number" min="0" step="1" defaultValue="0" required /></label>
        </div>
        <button className="button button-primary" disabled={busy}>Agregar combinación</button>
      </form>}
    </>}
  </section>
}

function SaleForm({ clients, alfajores, onSave }: { clients: Client[]; alfajores: Alfajor[]; onSave: (value: { clientId: number; alfajorId: number; presentationId: number; quantity: number; paymentMethod: string }) => Promise<void> }) {
  const [selectedAlfajorId, setSelectedAlfajorId] = useState(0)
  const selectedAlfajor = alfajores.find(item => item.id === selectedAlfajorId)
  return <form className="admin-form sale-form" onSubmit={event => { event.preventDefault(); const form = new FormData(event.currentTarget); void onSave({ clientId: Number(form.get('clientId')), alfajorId: selectedAlfajorId, presentationId: Number(form.get('presentationId')), quantity: Number(form.get('quantity')), paymentMethod: String(form.get('paymentMethod')) }) }}><h2>Nueva venta presencial</h2><div className="form-grid"><label>Cliente<select name="clientId" required><option value="">Elegí cliente</option>{clients.map(client => <option key={client.id} value={client.id}>{client.name} {client.lastName} · {client.dni}</option>)}</select></label><label>Alfajor<select name="alfajorId" required value={selectedAlfajorId || ''} onChange={event => setSelectedAlfajorId(Number(event.target.value))}><option value="">Elegí alfajor</option>{alfajores.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label><label>Presentación<select name="presentationId" required disabled={!selectedAlfajor}>{selectedAlfajor?.presentations.map(presentation => <option key={presentation.id} value={presentation.id}>{presentation.name} · {presentation.availability}</option>)}</select></label><label>Cantidad<input name="quantity" type="number" min="1" required /></label><label>Medio de pago<select name="paymentMethod" defaultValue="EFECTIVO"><option value="EFECTIVO">Efectivo</option><option value="TRANSFERENCIA">Transferencia</option><option value="TARJETA">Tarjeta</option><option value="MERCADO_PAGO">Mercado Pago</option></select></label></div><button className="button button-primary">Confirmar venta</button><p className="form-help">El backend valida el stock y calcula el importe. Solo una venta en efectivo queda pagada inmediatamente; los otros medios quedan pendientes.</p></form>
}

function LookupAdmin({ name, records, token, onRefresh }: { name: string; records: Lookup[]; token: string; onRefresh: () => void }) {
  const [error, setError] = useState('')
  const [form, setForm] = useState({ name: '', description: '' })
  const [units, setUnits] = useState(1)
  const route = name === 'presentaciones' ? 'presentaciones' : lookupRoute[name as keyof typeof lookupRoute]
  return <><form className="admin-form lookup-form" onSubmit={async event => { event.preventDefault(); try { await api(`/api/${route}`, token, { method: 'POST', body: { ...form, units } }); setForm({ name: '', description: '' }); setError(''); onRefresh() } catch (reason) { setError(reason instanceof Error ? reason.message : 'No se pudo guardar.') } }}><h2>Agregar {name.slice(0,-1)}</h2><div className="form-grid"><label>Nombre<input required value={form.name} onChange={event => setForm({ ...form, name: event.target.value })} /></label><label>Descripción<input value={form.description} onChange={event => setForm({ ...form, description: event.target.value })} /></label>{name === 'presentaciones' && <label>Unidades<input type="number" min="1" value={units} onChange={event => setUnits(Number(event.target.value))} /></label>}</div><button className="button button-primary">Guardar</button>{error && <p className="form-error">{error}</p>}</form><DataTable view={name as View} records={records as unknown as Record<string,unknown>[]} filter="" onFilter={() => undefined} role="ADMINISTRATIVO" /></>
}

export default App
