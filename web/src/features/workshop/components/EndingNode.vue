<script setup lang="ts">
import { Handle, Position } from '@vue-flow/core'

/** An ending of the scenario ("@victory", "@defeat"…): exits can lead to it. */
defineProps<{ data: { caption: string; tone: 'victory' | 'defeat' | 'other' } }>()
</script>

<template>
  <div class="ending" :class="data.tone">
    <Handle type="target" :position="Position.Left" class="in" />
    <i :class="data.tone === 'defeat' ? 'pi pi-times-circle' : 'pi pi-flag'" aria-hidden="true" />
    <strong>{{ data.caption }}</strong>
  </div>
</template>

<style scoped>
.ending {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  padding: 0.6rem 0.9rem;
  border-radius: 999px;
  border: 2px solid #a3a3a3;
  background: var(--p-content-background);
  color: var(--p-text-color);
  font-size: 0.85rem;
}
.ending.victory {
  border-color: #16a34a;
}
.ending.victory i {
  color: #16a34a;
}
.ending.defeat {
  border-color: #dc2626;
}
.ending.defeat i {
  color: #dc2626;
}
.in {
  width: 12px;
  height: 12px;
  background: var(--p-primary-color);
}
</style>
