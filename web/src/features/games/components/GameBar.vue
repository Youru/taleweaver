<script setup lang="ts">
import { computed, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import Badge from 'primevue/badge'
import Menu from 'primevue/menu'
import type { MenuItem } from 'primevue/menuitem'
import vTooltip from 'primevue/tooltip'
import { isHostDevice } from '@/shared/lib/screen'
import { openRules } from '@/features/reference'
import { usePreferences } from '@/shared/lib/preferences'

/**
 * The game's part of the top banner: its title, what the GM opens often as icons, the rest in one menu
 * (the GM's tools, then what the banner shows on the other pages).
 */
const props = defineProps<{
  title: string
  /** The Bounty being played. */
  bounty?: string
  preparing: boolean
  toHandOut: number
  requests: number
  revealed: number
  canUndo: boolean
  undoBlocked: boolean
  tools: MenuItem[]
}>()
const emit = defineEmits<{ undo: []; inventory: []; room: []; screens: []; sheet: [] }>()
const host = isHostDevice()

const { t } = useI18n()
const router = useRouter()
const { language } = storeToRefs(usePreferences())
const menu = useTemplateRef<{ toggle: (event: Event) => void }>('menu')
const items = computed<MenuItem[]>(() => [
  ...props.tools,
  { separator: true },
  ...(host ? [{ label: t('app.workshop'), icon: 'pi pi-map', command: () => void router.push({ name: 'workshop' }) }] : []),
  // Help, in another tab: the game stays where it is
  {
    label: t('help.thisPage'),
    icon: 'pi pi-question-circle',
    command: () => void window.open(router.resolve({ name: 'manual', hash: '#encounter' }).href, '_blank', 'noopener'),
  },
  {
    label: language.value === 'fr' ? 'English' : 'Français',
    icon: 'pi pi-language',
    command: () => (language.value = language.value === 'fr' ? 'en' : 'fr'),
  },
])
const inventoryLabel = computed(() =>
  props.toHandOut
    ? `${t('inventory.open')} (${t('inventory.toHandOut', { n: props.toHandOut })})`
    : t('inventory.open'),
)
</script>

<template>
  <div class="game-bar">
    <h1 :title="title">{{ title }}</h1>
    <span v-if="bounty" class="bounty" :title="bounty"><i class="pi pi-flag" aria-hidden="true" /> {{ bounty }}</span>
    <div class="actions">
      <!-- A disabled button shows no tooltip: the span carries the reason -->
      <span :title="undoBlocked ? t('play.undoBlocked') : undefined">
        <button
          v-tooltip.bottom="t('play.undo')"
          type="button"
          :aria-label="t('play.undo')"
          :disabled="!canUndo"
          @click="emit('undo')"
        >
          <i class="pi pi-undo" aria-hidden="true" />
        </button>
      </span>
      <span class="divider" />
      <button
        v-if="!preparing"
        v-tooltip.bottom="inventoryLabel"
        type="button"
        :aria-label="inventoryLabel"
        @click="emit('inventory')"
      >
        <i class="pi pi-briefcase" aria-hidden="true" />
        <Badge v-if="toHandOut" class="count" :value="toHandOut" severity="warn" />
      </button>
      <button
        v-tooltip.bottom="t('room.gm.button')"
        type="button"
        :aria-label="t('room.gm.button')"
        @click="emit('room')"
      >
        <i class="pi pi-mobile" aria-hidden="true" />
        <Badge v-if="requests" class="count" :value="requests" severity="warn" />
      </button>
      <button
        v-if="host"
        v-tooltip.bottom="t('screens.title')"
        type="button"
        class="screens"
        :aria-label="t('screens.title')"
        @click="emit('screens')"
      >
        <i class="pi pi-tablet" aria-hidden="true" />
        <span aria-hidden="true">{{ t('screens.short') }}</span>
      </button>
      <button v-tooltip.bottom="t('sheet.open')" type="button" :aria-label="t('sheet.open')" @click="emit('sheet')">
        <i class="pi pi-file" aria-hidden="true" />
        <Badge v-if="revealed" class="count" :value="revealed" severity="secondary" />
      </button>
      <button
        v-tooltip.bottom="t('reference.shortcut')"
        type="button"
        :aria-label="t('reference.open')"
        @click="openRules"
      >
        <i class="pi pi-question-circle" aria-hidden="true" />
      </button>
      <span class="divider" />
      <button
        v-tooltip.left="t('play.menu')"
        type="button"
        :aria-label="t('play.menu')"
        aria-haspopup="true"
        @click="menu?.toggle($event)"
      >
        <i class="pi pi-bars" aria-hidden="true" />
      </button>
      <Menu ref="menu" :model="items" popup />
    </div>
  </div>
</template>

<style scoped>
.game-bar,
.actions {
  display: flex;
  align-items: center;
  min-width: 0;
}
.game-bar {
  gap: 0.75rem;
}
h1,
.bounty {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--mid-cream);
}
h1 {
  margin: 0;
  padding-left: 0.75rem;
  border-left: 1px solid rgb(243 233 216 / 0.35);
  font-size: 1.1rem;
}
.bounty {
  font-size: 0.85rem;
}
.actions {
  flex: none;
  gap: 0.2rem;
  margin-left: auto;
}
button {
  position: relative;
  display: grid;
  place-items: center;
  width: 2.5rem;
  height: 2.5rem;
  padding: 0;
  border: 0;
  border-radius: 50%;
  background: transparent;
  color: var(--mid-cream);
  cursor: pointer;
}
.screens {
  display: flex;
  justify-content: center;
  gap: 0.35rem;
  width: auto;
  min-width: 2.5rem;
  padding: 0 0.6rem;
  border-radius: 1.25rem;
  font: inherit;
  font-size: 0.85rem;
}
@media (max-width: 70rem) {
  .screens span {
    display: none;
  }
}
button i {
  font-size: 1.1rem;
}
button:hover:not(:disabled) {
  background: rgb(0 0 0 / 0.22);
}
button:focus-visible {
  outline: 2px solid var(--mid-cream);
}
button:disabled {
  opacity: 0.4;
  cursor: default;
}
.count {
  position: absolute;
  top: 0;
  right: 0;
  min-width: 1.1rem;
  height: 1.1rem;
  font-size: 0.7rem;
  line-height: 1.1rem;
}
.divider {
  height: 1.4rem;
  margin: 0 0.35rem;
  border-left: 1px solid rgb(243 233 216 / 0.35);
}
@media (max-width: 40rem) {
  .game-bar {
    flex-wrap: wrap;
    row-gap: 0;
  }
  h1 {
    flex-basis: 100%;
    padding-left: 0;
    border-left: 0;
  }
  .divider {
    display: none;
  }
}
</style>
