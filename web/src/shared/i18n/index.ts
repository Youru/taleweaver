import { createI18n } from 'vue-i18n'
import { messages, type MessageSchema } from './messages'

export const i18n = createI18n<[MessageSchema], 'fr' | 'en', false>({
  legacy: false,
  locale: 'fr',
  fallbackLocale: 'en',
  messages: messages as unknown as Record<'fr' | 'en', MessageSchema>,
  datetimeFormats: {
    fr: { short: { dateStyle: 'short', timeStyle: 'short' } },
    en: { short: { dateStyle: 'short', timeStyle: 'short' } },
  },
})
