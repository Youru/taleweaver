/** Text in several languages, as stored in the content: { "en": "…", "fr": "…" }. */
export type LocalizedText = Record<string, string>

export type Language = 'fr' | 'en'

/** The text in the wanted language, else in English, else in any language. */
export function pick(text: LocalizedText | null | undefined, language: Language): string {
  if (!text) return ''
  return text[language] ?? text.en ?? Object.values(text)[0] ?? ''
}
