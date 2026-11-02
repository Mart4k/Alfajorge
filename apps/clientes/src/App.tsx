import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { ApiError, api } from './api'
import type { Account, Alfajor, CartLine, Promotion } from './api'
import './App.css'

type AuthMode = 'login' | 'register' | null
type Session = { token: string; user: Account }
type Order = { id: number; date: string; origin: string; status: string; total: number; client: string }
type OrderDetail = Order & { items: { alfajor: string; presentation: string; quantity: number; unitPrice: number; subtotal: number }[] }
type Invoice = { id: number; number: string; date: string; total: number }
type PlaceSelection = { address: string; locality: string; postalCode: string }
type GooglePlaceComponent = { longText?: string; types: string[] }
type GooglePlace = {
  formattedAddress?: string
  addressComponents?: GooglePlaceComponent[]
  fetchFields: (options: { fields: string[] }) => Promise<void>
}
type GooglePlaceAutocomplete = HTMLElement & {
  includedRegionCodes: string[]
  placeholder: string
}
type GoogleMapsPlacesLibrary = { PlaceAutocompleteElement: new () => GooglePlaceAutocomplete }

declare global {
  interface Window {
    google?: {
      maps?: {
        importLibrary: (name: 'places') => Promise<unknown>
      }
    }
  }
}

let googleMapsScriptPromise: Promise<void> | undefined

function loadGoogleMaps(apiKey: string): Promise<void> {
  if (window.google?.maps?.importLibrary) return Promise.resolve()
  if (!googleMapsScriptPromise) {
    googleMapsScriptPromise = new Promise<void>((resolve, reject) => {
      const script = document.createElement('script')
      script.src = `https://maps.googleapis.com/maps/api/js?key=${encodeURIComponent(apiKey)}&v=weekly&loading=async`
      script.async = true
      script.defer = true
      script.dataset.alfajorgeMaps = 'true'
      script.onload = () => window.google?.maps?.importLibrary
        ? resolve()
        : reject(new Error('Google Maps no quedó disponible.'))
      script.onerror = () => reject(new Error('No se pudo cargar Google Places.'))
      document.head.append(script)
    }).catch(error => {
      googleMapsScriptPromise = undefined
      throw error
    })
  }
  return googleMapsScriptPromise
}

const money = (value: number) => new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)
const readSession = (): Session | null => {
  try { return JSON.parse(localStorage.getItem('alfajorge-customer-session') || 'null') as Session | null }
  catch { return null }
}
const readCart = (): CartLine[] => {
  try { return JSON.parse(localStorage.getItem('alfajorge-customer-cart') || '[]') as CartLine[] }
  catch { return [] }
}

function App() {
  const [session, setSession] = useState<Session | null>(readSession)
  const [authMode, setAuthMode] = useState<AuthMode>(null)
  const [view, setView] = useState<'catalog' | 'promotions' | 'cart' | 'orders' | 'invoices' | 'account'>('catalog')
  const [products, setProducts] = useState<Alfajor[]>([])
  const [promotions, setPromotions] = useState<Promotion[]>([])
  const [catalogLoading, setCatalogLoading] = useState(true)
  const [catalogError, setCatalogError] = useState('')
  const [cart, setCart] = useState<CartLine[]>(readCart)
  const [orders, setOrders] = useState<Order[]>([])
  const [orderDetails, setOrderDetails] = useState<Record<number, OrderDetail>>({})
  const [loadingOrderId, setLoadingOrderId] = useState<number | null>(null)
  const [invoices, setInvoices] = useState<Invoice[]>([])
  const [search, setSearch] = useState('')
  const [filters, setFilters] = useState({ type: '', flavor: '', coverage: '' })
  const [paymentMethod, setPaymentMethod] = useState('EFECTIVO')
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [confirmCancelOrderId, setConfirmCancelOrderId] = useState<number | null>(null)

  useEffect(() => { localStorage.setItem('alfajorge-customer-cart', JSON.stringify(cart)) }, [cart])

  const refreshCatalog = useCallback(async () => {
    setCatalogLoading(true)
    setCatalogError('')
    try {
      const [alfajores, activePromotions] = await Promise.all([
        api<Alfajor[]>('/api/alfajores'),
        api<Promotion[]>('/api/promociones/activas'),
      ])
      setProducts(alfajores)
      setPromotions(activePromotions)
    } catch (error) {
      setCatalogError(error instanceof Error ? error.message : 'No se pudo cargar el catálogo.')
    } finally {
      setCatalogLoading(false)
    }
  }, [])

  const refreshCustomerData = useCallback(async (token: string) => {
    try {
      const [myOrders, myInvoices] = await Promise.all([
        api<Order[]>('/api/operaciones/mias', token),
        api<Invoice[]>('/api/facturas/mias', token),
      ])
      setOrders(myOrders)
      setInvoices(myInvoices)
    } catch (error) {
      if (error instanceof ApiError && (error.status === 401 || error.status === 403)) {
        setSession(null)
        localStorage.removeItem('alfajorge-customer-session')
      }
      setMessage(error instanceof Error ? error.message : 'No se pudieron cargar los datos de la cuenta.')
    }
  }, [])

  useEffect(() => {
    void refreshCatalog()
    if (session?.token) void refreshCustomerData(session.token)
  }, [session?.token, refreshCatalog, refreshCustomerData])

  const filteredProducts = useMemo(() => products.filter(product => {
    const text = `${product.name} ${product.description || ''} ${product.flavor} ${product.coverage}`.toLocaleLowerCase('es-AR')
    return text.includes(search.toLocaleLowerCase('es-AR')) && (!filters.type || product.type === filters.type) && (!filters.flavor || product.flavor === filters.flavor) && (!filters.coverage || product.coverage === filters.coverage)
  }), [products, search, filters])

  const totalItems = cart.reduce((sum, item) => sum + item.quantity, 0)
  const cartTotal = cart.reduce((sum, item) => sum + item.quantity * item.presentation.price, 0)

  async function signIn(values: Record<string, string>, registering: boolean) {
    setBusy(true); setMessage('')
    try {
      if (registering) await api('/api/auth/register', { method: 'POST', body: values })
      const result = await api<Session>('/api/auth/login', { method: 'POST', body: { email: values.email, password: values.password } })
      if (result.user.role !== 'CLIENTE') {
        setMessage('Esta cuenta pertenece al sistema interno. Ingresá desde el portal de empleados.')
        return
      }
      localStorage.setItem('alfajorge-customer-session', JSON.stringify(result))
      setSession(result); setAuthMode(null); setView('catalog')
    } catch (error) { setMessage(error instanceof Error ? error.message : 'No se pudo validar la cuenta.') }
    finally { setBusy(false) }
  }

  function addToCart(product: Alfajor, presentationId: number) {
    const presentation = product.presentations.find(item => item.id === presentationId)
    if (!presentation || presentation.availability === 'Sin stock') return
    setCart(current => {
      const match = current.find(item => item.alfajorId === product.id && item.presentationId === presentation.id)
      return match
        ? current.map(item => item === match ? { ...item, quantity: item.quantity + 1 } : item)
        : [...current, { alfajorId: product.id, presentationId: presentation.id, quantity: 1, alfajor: product, presentation }]
    })
    setMessage(`${product.name} agregado al carrito.`)
  }

  function changeQuantity(line: CartLine, delta: number) {
    setCart(current => current.map(item => item === line ? { ...item, quantity: Math.max(1, item.quantity + delta) } : item))
  }

  async function checkout(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!session) { setAuthMode('login'); setView('cart'); return }
    if (!cart.length) return
    setBusy(true); setMessage('')
    try {
      const result = await api<{ operationId: number; total: number; status: string }>('/api/operaciones/web', session.token, {
        method: 'POST',
        body: { items: cart.map(item => ({ alfajorId: item.alfajorId, presentationId: item.presentationId, quantity: item.quantity })), paymentMethod },
      })
      setCart([]); setView('orders'); setMessage(`Pedido #${result.operationId} registrado y pendiente de confirmación del pago. Total: ${money(result.total)}.`)
      await refreshCatalog(); await refreshCustomerData(session.token)
    } catch (error) { setMessage(error instanceof Error ? error.message : 'No se pudo confirmar el pedido.') }
    finally { setBusy(false) }
  }

  async function signOut() {
    localStorage.removeItem('alfajorge-customer-session')
    setSession(null); setOrders([]); setInvoices([]); setOrderDetails({}); setView('catalog'); setAuthMode(null)
    await refreshCatalog()
  }

  async function toggleOrderDetails(orderId: number) {
    if (!session?.token) return
    if (orderDetails[orderId]) {
      setOrderDetails(current => {
        const next = { ...current }
        delete next[orderId]
        return next
      })
      return
    }
    setLoadingOrderId(orderId)
    try {
      const details = await api<OrderDetail>(`/api/operaciones/${orderId}`, session.token)
      setOrderDetails(current => ({ ...current, [orderId]: details }))
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No se pudo cargar el detalle de la compra.')
    } finally {
      setLoadingOrderId(null)
    }
  }

  async function cancelOrder(orderId: number) {
    if (!session?.token) return
    setBusy(true)
    setMessage('')
    try {
      await api(`/api/operaciones/${orderId}/cancelar`, session.token, { method: 'POST' })
      await refreshCustomerData(session.token)
      setMessage(`La operación #${orderId} fue cancelada.`)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No se pudo cancelar la operación.')
    } finally {
      setBusy(false)
    }
  }

  if (authMode) return <AuthView mode={authMode} busy={busy} message={message} onMode={setAuthMode} onBack={() => { setAuthMode(null); setMessage('') }} onSubmit={signIn} />

  return (
    <div className="shop-shell">
      <header className="shop-header">
        <button className="shop-logo" onClick={() => setView('catalog')} aria-label="Alfajorge, inicio"><img src={`${import.meta.env.BASE_URL}assets/logo-alfajorge.jpeg`} alt="Alfajorge" /></button>
        <nav className="shop-nav" aria-label="Navegación principal">
          <button className={view === 'catalog' ? 'active' : ''} onClick={() => setView('catalog')}>Alfajores</button>
          <button className={view === 'promotions' ? 'active' : ''} onClick={() => setView('promotions')}>Promociones</button>
          {session && <><button className={view === 'orders' ? 'active' : ''} onClick={() => setView('orders')}>Mis compras</button><button className={view === 'invoices' ? 'active' : ''} onClick={() => setView('invoices')}>Facturas</button></>}
        </nav>
        <div className="shop-actions">
          <button className="cart-shortcut" onClick={() => setView('cart')}>Carrito <span>{totalItems}</span></button>
          {session ? <><button className="account-shortcut" onClick={() => setView('account')}>{session.user.name}</button><button className="text-action" onClick={() => void signOut()}>Salir</button></> : <><button className="text-action" onClick={() => setAuthMode('login')}>Ingresar</button><button className="button button-primary" onClick={() => setAuthMode('register')}>Crear cuenta</button></>}
          <a className="employee-link" href={import.meta.env.VITE_EMPLOYEES_URL || '/empleados/'}>Portal empleados</a>
        </div>
      </header>

      {view === 'catalog' && <>
        <section className="shop-hero"><div><span className="eyebrow">ALFAJORES ARTESANALES</span><h1>Un pequeño <code>[break]</code></h1><p>Elegí tu favorito y armá tu pedido.</p><button className="button button-gold" onClick={() => document.querySelector('#catalog')?.scrollIntoView({ behavior: 'smooth' })}>Explorar alfajores</button></div><div className="hero-stamp" aria-hidden="true"><span>HECHO CON TIEMPO</span><img src={`${import.meta.env.BASE_URL}assets/logo-alfajorge.jpeg`} alt="" /><span>COMPARTIDO CON GANAS</span></div></section>
        <main className="shop-content" id="catalog">
          <div className="section-title"><div><span className="eyebrow">EL MOSTRADOR</span><h2>Nuestros alfajores</h2></div><span className="result-count">{filteredProducts.length} variedades</span></div>
          <div className="catalog-tools"><input aria-label="Buscar alfajores" placeholder="Buscar por nombre o sabor" value={search} onChange={event => setSearch(event.target.value)} /><select aria-label="Filtrar por tipo" value={filters.type} onChange={event => setFilters({ ...filters, type: event.target.value })}><option value="">Todos los tipos</option>{unique(products.map(product => product.type)).map(value => <option key={value}>{value}</option>)}</select><select aria-label="Filtrar por sabor" value={filters.flavor} onChange={event => setFilters({ ...filters, flavor: event.target.value })}><option value="">Todos los sabores</option>{unique(products.map(product => product.flavor)).map(value => <option key={value}>{value}</option>)}</select><select aria-label="Filtrar por cobertura" value={filters.coverage} onChange={event => setFilters({ ...filters, coverage: event.target.value })}><option value="">Todas las coberturas</option>{unique(products.map(product => product.coverage)).map(value => <option key={value}>{value}</option>)}</select></div>
          {catalogLoading ? <div className="catalog-feedback" role="status">Cargando el catálogo…</div>
            : catalogError ? <div className="catalog-feedback catalog-error" role="alert"><strong>No pudimos conectar con el catálogo.</strong><p>{catalogError}</p><button className="button button-primary" onClick={() => void refreshCatalog()}>Reintentar</button></div>
              : filteredProducts.length ? <div className="product-grid">{filteredProducts.map(product => <ProductCard key={product.id} product={product} onAdd={addToCart} />)}</div>
                : products.length ? <EmptyState title="No encontramos alfajores con esos filtros" detail="Probá cambiar la búsqueda o quitar algún filtro." />
                  : <EmptyState title="Todavía no hay alfajores en el catálogo" detail="Cuando estén cargados desde el panel, van a aparecer acá." />}
        </main>
      </>}

      {view === 'promotions' && <main className="shop-content page-view"><span className="eyebrow">PARA COMPARTIR</span><h1>Promociones</h1>{promotions.length ? <div className="promotion-grid">{promotions.map(promotion => <article className="promotion-card" key={promotion.id}><span className="eyebrow">VÁLIDA HASTA {formatDate(promotion.ends)}</span><h2>{promotion.name}</h2><p>{promotion.description}</p><strong>{promotion.discount}% menos</strong></article>)}</div> : <EmptyState title="No hay promociones activas" detail="Volvé pronto para descubrir nuevas combinaciones." />}</main>}

      {view === 'cart' && <main className="shop-content page-view"><span className="eyebrow">TU PEDIDO</span><h1>Carrito</h1>{cart.length ? <><div className="cart-list">{cart.map(line => <article className="cart-line" key={`${line.alfajorId}-${line.presentationId}`}><CartThumbnail product={line.alfajor} /><div className="cart-product-info"><strong>{line.alfajor.name}</strong><span>{line.presentation.name} · {money(line.presentation.price)}</span></div><div className="quantity-control"><button type="button" onClick={() => changeQuantity(line, -1)} aria-label="Quitar una unidad">−</button><span>{line.quantity}</span><button type="button" onClick={() => changeQuantity(line, 1)} aria-label="Agregar una unidad">＋</button></div><strong>{money(line.quantity * line.presentation.price)}</strong><button type="button" className="remove-link" aria-label={`Quitar ${line.alfajor.name} del carrito`} onClick={() => setCart(current => current.filter(item => item !== line))}>Quitar</button></article>)}</div><form className="checkout-box" onSubmit={event => void checkout(event)}><label htmlFor="payment">Medio de pago</label><select id="payment" value={paymentMethod} onChange={event => setPaymentMethod(event.target.value)}><option value="EFECTIVO">Efectivo</option><option value="TRANSFERENCIA">Transferencia</option><option value="TARJETA">Tarjeta</option><option value="MERCADO_PAGO">Mercado Pago</option></select><div className="checkout-total"><span>Total del pedido</span><strong>{money(cartTotal)}</strong></div><button className="button button-primary" disabled={busy}>{busy ? 'Confirmando…' : session ? 'Confirmar compra' : 'Ingresar para continuar'}</button><small>El servidor confirma precios y disponibilidad antes de aceptar el pedido.</small></form></> : <EmptyState title="Tu carrito está vacío" detail="Elegí tus alfajores favoritos para empezar." />}</main>}

      {view === 'orders' && <main className="shop-content page-view"><span className="eyebrow">TU CUENTA</span><h1>Mis compras</h1>{orders.length ? <div className="history-list">{orders.map(order => <article key={order.id}><div><strong>Operación #{order.id}</strong><span>{formatDate(order.date)} · {order.origin}</span>{orderDetails[order.id] && <ul className="order-detail-list">{orderDetails[order.id].items.map((item, index) => <li key={`${item.alfajor}-${item.presentation}-${index}`}><span>{item.quantity} × {item.alfajor} · {item.presentation}</span><strong>{money(item.subtotal)}</strong></li>)}</ul>}</div><span className={`status status-${order.status.toLowerCase()}`}>{order.status}</span><strong>{money(order.total)}</strong><div className="order-actions"><button className="order-detail-toggle" type="button" disabled={loadingOrderId === order.id} onClick={() => void toggleOrderDetails(order.id)}>{loadingOrderId === order.id ? 'Cargando…' : orderDetails[order.id] ? 'Ocultar detalle' : 'Ver detalle'}</button>{order.status === 'PENDIENTE' && <button className="remove-link" type="button" disabled={busy} onClick={() => { if (confirmCancelOrderId === order.id) { setConfirmCancelOrderId(null); void cancelOrder(order.id) } else setConfirmCancelOrderId(order.id) }}>{confirmCancelOrderId === order.id ? 'Confirmar cancelación' : 'Cancelar pedido'}</button>}</div></article>)}</div> : <EmptyState title="Todavía no tenés compras" detail="Cuando confirmes tu primer pedido, lo vas a ver acá." />}</main>}

      {view === 'invoices' && <main className="shop-content page-view"><span className="eyebrow">DOCUMENTACIÓN</span><h1>Mis facturas</h1>{invoices.length ? <div className="history-list">{invoices.map(invoice => <article key={invoice.id}><div><strong>{invoice.number}</strong><span>{formatDate(invoice.date)}</span></div><strong>{money(invoice.total)}</strong></article>)}</div> : <EmptyState title="Todavía no hay facturas" detail="Se mostrarán cuando tus compras estén pagadas." />}</main>}

      {view === 'account' && session && <main className="shop-content page-view"><span className="eyebrow">TU PERFIL</span><h1>Mi cuenta</h1><div className="profile-card"><p><span>Nombre</span><strong>{session.user.name} {session.user.lastName}</strong></p><p><span>Correo</span><strong>{session.user.email}</strong></p></div></main>}

      {message && <div className="shop-toast" role="status"><span>{message}</span><button onClick={() => setMessage('')} aria-label="Cerrar aviso">×</button></div>}
      <footer className="shop-footer"><span>Alfajorge · Un pequeño descanso</span><span>Hecho con tiempo · Compartido con ganas</span></footer>
    </div>
  )
}

function ProductCard({ product, onAdd }: { product: Alfajor; onAdd: (product: Alfajor, presentationId: number) => void }) {
  const [presentationId, setPresentationId] = useState(product.presentations[0]?.id || 0)
  const selected = product.presentations.find(item => item.id === presentationId)
  const unavailable = selected?.availability === 'Sin stock'
  return <article className="product-card"><div className="product-image">{product.imageUrl ? <img src={product.imageUrl} alt={product.name} loading="lazy" /> : <div className="wrapper"><span>ALFAJORGE</span><strong>{product.name.slice(0, 1)}</strong><small>{product.coverage}</small></div>}</div><div className="product-info"><div className="product-tags"><span>{product.type}</span><span>{product.flavor}</span></div><h3>{product.name}</h3><p>{product.description || `${product.flavor} · ${product.coverage}`}</p><label htmlFor={`presentation-${product.id}`}>Presentación</label><select id={`presentation-${product.id}`} value={presentationId} onChange={event => setPresentationId(Number(event.target.value))}>{product.presentations.map(item => <option key={item.id} value={item.id}>{item.name} · {item.units} u · {money(item.price)}</option>)}</select><div className="product-action"><div><strong>{money(selected?.price || 0)}</strong><span className={unavailable ? 'availability unavailable' : 'availability'}>{selected?.availability}</span></div><button className="button button-primary" disabled={!selected || unavailable} onClick={() => selected && onAdd(product, selected.id)}>Agregar</button></div></div></article>
}

function CartThumbnail({ product }: { product: Alfajor }) {
  const [imageFailed, setImageFailed] = useState(false)
  return product.imageUrl && !imageFailed
    ? <img className="cart-product-image" src={product.imageUrl} alt={product.name} onError={() => setImageFailed(true)} />
    : <div className="cart-product-placeholder" aria-label={`Imagen de ${product.name}`}>{product.name.slice(0, 1)}</div>
}

function AuthView({ mode, busy, message, onMode, onBack, onSubmit }: { mode: Exclude<AuthMode, null>; busy: boolean; message: string; onMode: (mode: AuthMode) => void; onBack: () => void; onSubmit: (values: Record<string, string>, registering: boolean) => Promise<void> }) {
  const registering = mode === 'register'
  const [address, setAddress] = useState('')
  const [city, setCity] = useState('')
  const [postalCode, setPostalCode] = useState('')
  const onPlaceSelected = useCallback((selection: PlaceSelection) => {
    setAddress(selection.address)
    if (selection.locality) setCity(selection.locality)
    if (selection.postalCode) setPostalCode(selection.postalCode)
  }, [])
  const googleMapsApiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY
  return <main className="auth-page"><div className="auth-brand"><button onClick={onBack} className="shop-logo"><img src={`${import.meta.env.BASE_URL}assets/logo-alfajorge.jpeg`} alt="Alfajorge" /></button><div className="auth-brand-copy"><span className="eyebrow">ALFAJORES ARTESANALES</span><h1>Una pausa hecha <em>para compartir.</em></h1><p>Elegí tus favoritos y armá un pedido a tu manera.</p></div></div><section className="auth-card"><span className="eyebrow">CUENTA ALFAJORGE</span><h2>{registering ? 'Crear cuenta' : 'Ingresar'}</h2><p>{registering ? 'Tus datos quedan asociados a tus compras.' : 'Ingresá para continuar con tu pedido.'}</p><form onSubmit={event => { event.preventDefault(); void onSubmit(Object.fromEntries(new FormData(event.currentTarget).entries()) as Record<string, string>, registering) }}>{registering && <><label>Nombre<input name="name" required autoComplete="given-name" /></label><label>Apellido<input name="lastName" required autoComplete="family-name" /></label><label>DNI<input name="dni" required autoComplete="off" /></label></>}<label>Correo electrónico<input name="email" type="email" required autoComplete="username" /></label><label>Contraseña<input name="password" type="password" required minLength={10} autoComplete={registering ? 'new-password' : 'current-password'} /></label>{registering && <><label>Teléfono<input name="phone" type="tel" autoComplete="tel" /></label><label>Dirección{googleMapsApiKey && <PlacesAddressInput apiKey={googleMapsApiKey} onPlaceSelected={onPlaceSelected} />}<input name="address" value={address} onChange={event => setAddress(event.target.value)} autoComplete="street-address" /></label><label>Localidad<input name="city" value={city} onChange={event => setCity(event.target.value)} autoComplete="address-level2" /></label><label>Código postal<input name="postalCode" value={postalCode} onChange={event => setPostalCode(event.target.value)} autoComplete="postal-code" /></label></>}<button className="button button-primary auth-submit" disabled={busy}>{busy ? 'Procesando…' : registering ? 'Crear cuenta cliente' : 'Ingresar'}</button>{message && <p className="form-message" role="alert">{message}</p>}</form><button className="auth-toggle" onClick={() => onMode(registering ? 'login' : 'register')}>{registering ? 'Ya tengo una cuenta · Ingresar' : '¿Primera vez? · Crear cuenta'}</button>  <a className="employee-portal-link" href={import.meta.env.VITE_EMPLOYEES_URL || '/empleados/'}>¿Sos parte del equipo? Ingresar al portal interno</a></section></main>
}

function PlacesAddressInput({ apiKey, onPlaceSelected }: { apiKey: string; onPlaceSelected: (selection: PlaceSelection) => void }) {
  const containerRef = useRef<HTMLDivElement>(null)
  const [loadError, setLoadError] = useState('')

  useEffect(() => {
    let cancelled = false
    let autocomplete: GooglePlaceAutocomplete | undefined
    const handleSelect = async (event: Event) => {
      try {
        const selectedEvent = event as Event & {
          placePrediction?: { toPlace: () => GooglePlace }
          detail?: { placePrediction?: { toPlace: () => GooglePlace } }
        }
        const placePrediction = selectedEvent.placePrediction || selectedEvent.detail?.placePrediction
        if (!placePrediction) return
        const place = placePrediction.toPlace()
        await place.fetchFields({ fields: ['formattedAddress', 'addressComponents'] })
        const components = place.addressComponents || []
        const component = (type: string) => components.find(item => item.types.includes(type))?.longText || ''
        onPlaceSelected({
          address: place.formattedAddress || '',
          locality: component('locality') || component('postal_town') || component('administrative_area_level_2'),
          postalCode: component('postal_code'),
        })
        setLoadError('')
      } catch (error) {
        setLoadError(error instanceof Error ? error.message : 'No se pudo obtener esa dirección.')
      }
    }

    void loadGoogleMaps(apiKey).then(async () => {
      const maps = window.google?.maps
      if (!maps) throw new Error('Google Maps no quedó disponible.')
      const places = await maps.importLibrary('places') as GoogleMapsPlacesLibrary
      if (cancelled || !containerRef.current) return
      autocomplete = new places.PlaceAutocompleteElement()
      autocomplete.includedRegionCodes = ['ar']
      autocomplete.placeholder = 'Buscá una dirección en Argentina'
      autocomplete.addEventListener('gmp-select', handleSelect)
      containerRef.current.replaceChildren(autocomplete)
    }).catch(error => {
      if (!cancelled) setLoadError(error instanceof Error ? error.message : 'No se pudo cargar Google Places.')
    })

    return () => {
      cancelled = true
      autocomplete?.removeEventListener('gmp-select', handleSelect)
      autocomplete?.remove()
    }
  }, [apiKey, onPlaceSelected])

  return <><div className="google-place-picker" ref={containerRef} />{loadError && <span className="places-fallback">No se pudo cargar Google Places. Podés escribir la dirección manualmente.</span>}</>
}

function EmptyState({ title, detail }: { title: string; detail: string }) { return <div className="empty-state"><span className="empty-mark"><img src={`${import.meta.env.BASE_URL}assets/logo-alfajorge.jpeg`} alt="" /></span><h2>{title}</h2><p>{detail}</p></div> }
function unique(values: string[]) { return [...new Set(values.filter(Boolean))] }
function formatDate(value: string) { return new Intl.DateTimeFormat('es-AR', { dateStyle: 'medium' }).format(new Date(value)) }

export default App
