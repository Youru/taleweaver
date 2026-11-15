/** "72 %" (fr) or "72%" (en). */
export function percent(p: number, language: string): string {
  return new Intl.NumberFormat(language, { style: 'percent', maximumFractionDigits: 0 }).format(p)
}

/** "4,1" (fr) or "4.1" (en). */
export function decimal(value: number, language: string): string {
  return new Intl.NumberFormat(language, { maximumFractionDigits: 1 }).format(value)
}

/** The number in a field as it is typed (PrimeVue's InputNumber only updates its model on blur). */
export function typed(value: unknown): number | null {
  return typeof value === 'number' ? value : null
}
