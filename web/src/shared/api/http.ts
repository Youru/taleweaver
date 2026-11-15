/** RFC 9457 problem details, as returned by the API (title = stable error code). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  code?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails | null,
  ) {
    super(problem?.detail ?? problem?.title ?? `HTTP ${status}`)
    this.name = 'ApiError'
  }

  /** Stable error code ("Games.NotFound"…), when the API gave one. */
  get code(): string | undefined {
    return this.problem?.code ?? this.problem?.title
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
}

/** The only place that calls fetch: JSON in and out, ApiError on any non-2xx answer. */
export async function http<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await fetch(path, {
    method: options.method ?? 'GET',
    headers:
      options.body === undefined
        ? { Accept: 'application/json' }
        : { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    signal: options.signal,
  })
  if (!response.ok) {
    let problem: ProblemDetails | null = null
    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      problem = null
    }
    throw new ApiError(response.status, problem)
  }
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}
