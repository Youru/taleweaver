import { definePreset } from '@primeuix/themes'
import Aura from '@primeuix/themes/aura'

/**
 * Aura dressed like the Middara books: crimson of the encounter banners, sepia ink and parchment.
 * The page itself shows the rulebook's parchment (global.css): panels are lightly translucent paper over it.
 */
const crimson = {
  50: '#fbf1f0',
  100: '#f3d9d6',
  200: '#e4aea8',
  300: '#cf7d74',
  400: '#b2493f',
  500: '#8f2a22',
  600: '#7a211b',
  700: '#6d1a1a',
  800: '#521313',
  900: '#3a0d0d',
  950: '#240707',
}

const sepia = {
  0: '#fffaf0',
  50: '#f8f0e0',
  100: '#efe3c8',
  200: '#e3d2ae',
  300: '#cdb68c',
  400: '#a8916a',
  500: '#857153',
  600: '#665640',
  700: '#4b3f30',
  800: '#342b21',
  900: '#221c15',
  950: '#15110c',
}

export const TaleweaverPreset = definePreset(Aura, {
  semantic: {
    primary: crimson,
    colorScheme: {
      light: {
        surface: sepia,
        primary: {
          color: '{primary.700}',
          contrastColor: '#f3e9d8',
          hoverColor: '{primary.800}',
          activeColor: '{primary.900}',
        },
        highlight: {
          background: '{primary.100}',
          focusBackground: '{primary.200}',
          color: '{primary.800}',
          focusColor: '{primary.900}',
        },
        text: {
          color: '#2a2017',
          hoverColor: '#1d1712',
          mutedColor: '#6b5a45',
          hoverMutedColor: '#4b3f30',
        },
        content: {
          background: 'rgba(251, 245, 232, 0.72)',
          hoverBackground: 'rgba(227, 210, 174, 0.45)',
          borderColor: '#cdb68c',
          color: '{text.color}',
          hoverColor: '{text.hover.color}',
        },
        formField: {
          background: '#fbf5e8',
          disabledBackground: '{surface.100}',
          filledBackground: '{surface.50}',
          borderColor: '#bba37a',
          hoverBorderColor: '{surface.500}',
        },
        overlay: {
          select: { background: '#f8f0e0', borderColor: '#cdb68c' },
          popover: { background: '#f8f0e0', borderColor: '#cdb68c' },
          modal: { background: '#f8f0e0', borderColor: '#cdb68c' },
        },
      },
    },
  },
})
