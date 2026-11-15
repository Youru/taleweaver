import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { VueQueryPlugin } from '@tanstack/vue-query'
import PrimeVue from 'primevue/config'
import ConfirmationService from 'primevue/confirmationservice'
import 'primeicons/primeicons.css'
import { queryClient } from '@/shared/api/queryClient'
import { i18n } from '@/shared/i18n'
import App from './App.vue'
import { router } from './router'
import { TaleweaverPreset } from './theme'
import './global.css'

createApp(App)
  .use(createPinia())
  .use(router)
  .use(i18n)
  .use(VueQueryPlugin, { queryClient })
  .use(PrimeVue, { theme: { preset: TaleweaverPreset } })
  .use(ConfirmationService)
  .mount('#app')
