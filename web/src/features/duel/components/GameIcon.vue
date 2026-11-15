<script setup lang="ts">
/** A small game icon (symbol, stat, die): decorative unless it has a label. */
defineProps<{ src: string | undefined; label?: string; size?: string }>()
</script>

<template>
  <img
    v-if="src"
    class="icon"
    :src="src"
    :alt="label ?? ''"
    :aria-hidden="label ? undefined : 'true'"
    :style="{ height: size ?? '1.1em' }"
    draggable="false"
  />
</template>

<style scoped>
.icon {
  width: auto;
  vertical-align: -0.2em;
  user-select: none;
}
</style>
