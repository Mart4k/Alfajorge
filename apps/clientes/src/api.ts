export const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5184'

export type Account = {
  id: number
  name: string
  lastName: string
  email: string
  role: string
}

export type Presentation = {
  id: number
  name: string
  units: number
  price: number
  availability: string
}

export type Alfajor = {
  id: number
  name: string
  description: string | null
  imageUrl: string | null
  type: string
  flavor: string
  coverage: string
  presentations: Presentation[]
}

export type Promotion = {
  id: number
  name: string
  description: string
  discount: number
  starts: string
  ends: string
  alfajores: number[]
}

export type CartLine = {
  alfajorId: number
  presentationId: number
  quantity: number
  alfajor: Alfajor
  presentation: Presentation
}

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
  const options = typeof tokenOrOptions === 'string' ? authenticatedOptions : tokenOrOptions || {}
  const response = await fetch(`${API_URL}${path}`, {
    ...options,
    body: options.body === undefined ? undefined : typeof options.body === 'string' ? options.body : JSON.stringify(options.body),
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  })
  const body = await response.json().catch(() => null)
  if (!response.ok) throw new ApiError(body?.error || `No se pudo completar la solicitud (${response.status}).`, response.status)
  return body as T
}
