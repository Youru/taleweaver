import { definePreset } from '@primeuix/themes'
import Aura from '@primeuix/themes/aura'

/** Aura with a deep amber primary, for a parchment-and-lantern feel. */
export const TaleweaverPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '{amber.50}',
      100: '{amber.100}',
      200: '{amber.200}',
      300: '{amber.300}',
      400: '{amber.400}',
      500: '{amber.600}',
      600: '{amber.700}',
      700: '{amber.800}',
      800: '{amber.900}',
      900: '{amber.950}',
      950: '{amber.950}',
    },
  },
})
