import { useI18n } from 'vue-i18n'
import { ApiError } from '@/shared/api/http'

/** An error of the API in the reader's language when its code is known, else as it came. */
export function useErrorText() {
  const { t, te } = useI18n()
  function ofCode(code: string | null | undefined): string | undefined {
    if (!code) return undefined
    const key = `errors.${code.replace('.', '_')}`
    return te(key) ? t(key) : undefined
  }
  function textOf(error: unknown): string | null {
    if (!error) return null
    if (error instanceof ApiError) return ofCode(error.code) ?? error.message
    return error instanceof Error ? error.message : String(error)
  }
  return { ofCode, textOf }
}
