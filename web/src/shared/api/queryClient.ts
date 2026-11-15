import { QueryClient } from '@tanstack/vue-query'
import { ApiError } from './http'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      // A 4xx will not get better by retrying.
      retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2,
    },
  },
})
