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

async function failure(response: Response): Promise<ApiError> {
  let problem: ProblemDetails | null = null
  try {
    problem = (await response.json()) as ProblemDetails
  } catch {
    problem = null
  }
  return new ApiError(response.status, problem)
}

/** A file from the API (a PDF…), whole, with the name the API gave it; ApiError on any non-2xx answer. */
export async function httpFile(path: string, signal?: AbortSignal): Promise<{ blob: Blob; fileName?: string }> {
  const response = await fetch(path, { signal })
  if (!response.ok) throw await failure(response)
  const disposition = response.headers.get('Content-Disposition') ?? ''
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1]
  const plain = /filename="?([^";]+)"?/i.exec(disposition)?.[1]
  return { blob: await response.blob(), fileName: encoded ? decodeURIComponent(encoded) : plain }
}

/** Hands a file to the browser as a download (it shows once complete). */
export function saveFile(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 10_000)
}

/** The only place that calls fetch for JSON: JSON in and out, ApiError on any non-2xx answer. */
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
  if (!response.ok) throw await failure(response)
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}
