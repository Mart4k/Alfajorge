export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5184'
export type SessionUser = { id: number; name: string; lastName: string; email: string; role: 'ADMINISTRATIVO' | 'VENDEDOR' | 'CLIENTE' }
export type Session = { token: string; user: SessionUser }
export type Presentation = { id: number; name: string; units: number; price: number; availability: string }
export type Alfajor = { id: number; name: string; description: string | null; imageUrl: string | null; type: string; flavor: string; coverage: string; presentations: Presentation[] }
export type StockItem = { alfajorId: number; presentationId: number; alfajor: string; presentation: string; quantity: number; minimum: number; updated: string }
export type Client = { id: number; dni: string; name: string; lastName: string; email: string; phone: string | null; address: string | null; city: string | null; postalCode: string | null }
export type Employee = { id: number; dni: string; name: string; lastName: string; email: string; phone: string | null; cargo: string; isSeller: boolean }
export type Operation = { id: number; date: string; origin: string; status: string; total: number; client: string }
export type Payment = { id: number; operationId: number; method: string; amount: number; status: string; reference: string | null; date: string }
export type Invoice = { id: number; number: string; date: string; total: number }
export type Lookup = { id: number; name: string; description: string | null }

type ApiOptions = Omit<RequestInit, 'body'> & { body?: unknown }

export class ApiError extends Error {
  readonly status: number

  constructor(message: string, status: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export async function api<T>(path: string, tokenOrOptions?: string | ApiOptions, authenticatedOptions: ApiOptions = {}): Promise<T> {
  const token = typeof tokenOrOptions === 'string' ? tokenOrOptions : undefined
  const options = typeof tokenOrOptions === 'string' ? authenticatedOptions : tokenOrOptions || authenticatedOptions
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    body: options.body === undefined ? undefined : typeof options.body === 'string' ? options.body : JSON.stringify(options.body),
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, ...options.headers },
  })
  if (response.status === 204) return null as T
  const body = await response.json().catch(() => null)
  if (!response.ok) throw new ApiError(body?.error || `Solicitud rechazada (${response.status}).`, response.status)
  return body as T
}
