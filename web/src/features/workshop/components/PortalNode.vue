<script setup lang="ts">
import { Handle, Position } from '@vue-flow/core'

/**
 * A step of another chapter: an entry (on the left, it leads into this chapter) or an exit (on the
 * right, a link of this chapter leads to it). A click opens its chapter.
 */
defineProps<{ data: { direction: 'in' | 'out'; caption: string; chapter: string; title: string } }>()
</script>

<template>
  <div class="portal" :class="data.direction === 'in' ? 'entry' : 'exit'" :title="data.title">
    <Handle v-if="data.direction === 'out'" type="target" :position="Position.Left" class="handle" />
    <i :class="data.direction === 'in' ? 'pi pi-sign-in' : 'pi pi-sign-out'" aria-hidden="true" />
    <span class="text">
      <small>{{ data.chapter }}</small>
      <strong>{{ data.caption }}</strong>
    </span>
    <Handle
      v-if="data.direction === 'in'"
      type="source"
      :position="Position.Right"
      class="handle"
      :connectable="false"
    />
  </div>
</template>

<style scoped>
.portal {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  max-width: 14rem;
  padding: 0.5rem 0.8rem;
  border: 2px dashed var(--p-primary-color);
  border-radius: 0.75rem;
  background: color-mix(in srgb, var(--p-primary-color) 8%, var(--p-content-background));
  color: var(--p-text-color);
  font-size: 0.85rem;
  cursor: pointer;
}
.portal i {
  color: var(--p-primary-color);
}
.text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}
.text small {
  color: var(--p-text-muted-color);
  font-size: 0.7rem;
}
.text strong {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.handle {
  width: 12px;
  height: 12px;
  background: var(--p-primary-color);
}
</style>
