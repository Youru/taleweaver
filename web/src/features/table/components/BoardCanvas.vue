<script setup lang="ts">
import { computed, ref, useId, useTemplateRef, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import type { Cell } from '@/features/games'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { BoardLayout, FigureShape, Selection } from '../lib/layout'
import { cellAt, fitBox, panBox, zoomBox, type ViewBox } from '../lib/viewport'

/**
 * Draws the board and turns pointer gestures into intentions: select something, move a figure
 * (drag it, or tap a square), pan (drag the background), zoom (wheel, buttons). No rules here.
 */
const props = defineProps<{
  layout: BoardLayout
  selected: Selection | null
  label: string
}>()
const emit = defineEmits<{
  select: [selection: Selection | null]
  move: [figure: string, to: Cell]
  /** A tap on a square with nothing to select. */
  tap: [cell: Cell]
}>()

/** SVG units per square. */
const S = 100

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const svg = useTemplateRef<SVGSVGElement>('svg')
const clipId = `figure-clip-${useId()}`
const fogId = `fog-${useId()}`

const full = computed(() => fitBox(props.layout.width * S, props.layout.height * S, S / 4))
const box = ref<ViewBox>(full.value)
// A new board (encounter, diagram placed): show it whole.
watch(
  () => [props.layout.width, props.layout.height],
  () => (box.value = full.value),
)

function toSvg(event: { clientX: number; clientY: number }) {
  const ctm = svg.value?.getScreenCTM()
  if (!ctm) return { x: 0, y: 0 }
  const point = new DOMPoint(event.clientX, event.clientY).matrixTransform(ctm.inverse())
  return { x: point.x, y: point.y }
}

function zoom(factor: number, center = { x: box.value.x + box.value.w / 2, y: box.value.y + box.value.h / 2 }) {
  box.value = zoomBox(box.value, factor, center, full.value)
}

function onWheel(event: WheelEvent) {
  zoom(event.deltaY < 0 ? 1.2 : 1 / 1.2, toSvg(event))
}

interface Gesture {
  pointerId: number
  clientX: number
  clientY: number
  /** Pixels per SVG unit when the gesture started. */
  scale: number
  startBox: ViewBox
  target: Selection | null
  moved: boolean
}
let gesture: Gesture | null = null
/** Where the dragged figure is, in SVG units. */
const drag = ref<{ id: string; x: number; y: number } | null>(null)

function targetOf(element: EventTarget | null): Selection | null {
  const found = (element as Element | null)?.closest?.('[data-figure],[data-token],[data-area]')
  if (!found) return null
  const figure = found.getAttribute('data-figure')
  if (figure) return { kind: 'figure', id: figure }
  const token = found.getAttribute('data-token')
  if (token) return { kind: 'token', id: token }
  return { kind: 'area', id: found.getAttribute('data-area') ?? '' }
}

function onPointerDown(event: PointerEvent) {
  if (event.pointerType === 'mouse' && event.button !== 0) return
  svg.value?.setPointerCapture?.(event.pointerId)
  gesture = {
    pointerId: event.pointerId,
    clientX: event.clientX,
    clientY: event.clientY,
    scale: svg.value?.getScreenCTM()?.a ?? 1,
    startBox: box.value,
    target: targetOf(event.target),
    moved: false,
  }
}

function onPointerMove(event: PointerEvent) {
  if (!gesture || gesture.pointerId !== event.pointerId) return
  const dx = event.clientX - gesture.clientX
  const dy = event.clientY - gesture.clientY
  if (!gesture.moved && Math.hypot(dx, dy) < 6) return
  gesture.moved = true
  if (gesture.target?.kind === 'figure') {
    drag.value = { id: gesture.target.id, ...toSvg(event) }
  } else {
    box.value = panBox(gesture.startBox, dx / gesture.scale, dy / gesture.scale)
  }
}

function onPointerUp(event: PointerEvent) {
  if (!gesture || gesture.pointerId !== event.pointerId) return
  const { target, moved } = gesture
  gesture = null
  if (target?.kind === 'figure' && moved && drag.value) {
    emit('move', target.id, cellAt(drag.value, S))
    emit('select', target)
  } else if (!moved) {
    if (target) emit('select', target)
    else emit('tap', cellAt(toSvg(event), S))
  }
  drag.value = null
}

function onPointerCancel() {
  gesture = null
  drag.value = null
}

function isSelected(kind: Selection['kind'], id: string) {
  return props.selected?.kind === kind && props.selected.id === id
}

function figureTransform(figure: FigureShape) {
  if (drag.value?.id !== figure.id) return undefined
  const half = (figure.size * S) / 2
  return `translate(${drag.value.x - figure.x * S - half} ${drag.value.y - figure.y * S - half})`
}

function select(selection: Selection) {
  emit('select', selection)
}
</script>

<template>
  <div class="canvas">
    <div class="tools">
      <Button
        icon="pi pi-plus"
        severity="secondary"
        text
        rounded
        size="small"
        :aria-label="t('table.zoomIn')"
        @click="zoom(1.4)"
      />
      <Button
        icon="pi pi-minus"
        severity="secondary"
        text
        rounded
        size="small"
        :aria-label="t('table.zoomOut')"
        @click="zoom(1 / 1.4)"
      />
      <Button
        icon="pi pi-expand"
        severity="secondary"
        text
        rounded
        size="small"
        :aria-label="t('table.fit')"
        @click="box = full"
      />
    </div>
    <svg
      ref="svg"
      :viewBox="`${box.x} ${box.y} ${box.w} ${box.h}`"
      preserveAspectRatio="xMidYMid meet"
      role="img"
      :aria-label="label"
      @wheel.prevent="onWheel"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
      @pointercancel="onPointerCancel"
    >
      <defs>
        <clipPath :id="clipId" clipPathUnits="objectBoundingBox">
          <circle cx="0.5" cy="0.5" r="0.5" />
        </clipPath>
        <pattern :id="fogId" width="40" height="40" patternUnits="userSpaceOnUse" patternTransform="rotate(35)">
          <rect width="40" height="40" fill="#14161a" />
          <rect width="20" height="40" fill="#1b1e24" />
        </pattern>
      </defs>

      <g v-for="tile in layout.tiles" :key="tile.key" class="tile">
        <template v-if="tile.hidden">
          <g
            class="fog"
            :class="{ selected: isSelected('area', tile.hidden.id) }"
            :data-area="tile.hidden.id"
            role="button"
            tabindex="0"
            :aria-label="t('table.hiddenArea', { name: pick(tile.hidden.name, language) || tile.hidden.id })"
            @keydown.enter="select({ kind: 'area', id: tile.hidden.id })"
          >
            <rect :x="tile.x * S" :y="tile.y * S" :width="tile.w * S" :height="tile.h * S" :fill="`url(#${fogId})`" />
            <text :x="(tile.x + tile.w / 2) * S" :y="(tile.y + tile.h / 2) * S">?</text>
          </g>
        </template>
        <template v-else>
          <title>{{ t('table.tile', { name: tile.label }) }}</title>
          <image
            v-if="tile.href"
            :href="tile.href"
            :x="tile.x * S"
            :y="tile.y * S"
            :width="tile.w * S"
            :height="tile.h * S"
            preserveAspectRatio="none"
            :transform="`rotate(${tile.rotation} ${(tile.x + tile.w / 2) * S} ${(tile.y + tile.h / 2) * S})`"
          />
          <rect v-else class="missing" :x="tile.x * S" :y="tile.y * S" :width="tile.w * S" :height="tile.h * S" />
        </template>
        <rect class="tile-edge" :x="tile.x * S" :y="tile.y * S" :width="tile.w * S" :height="tile.h * S" />
      </g>

      <g v-for="area in layout.elevation" :key="area.key" class="elevation">
        <rect :x="area.x * S" :y="area.y * S" :width="area.w * S" :height="area.h * S" />
        <text :x="(area.x + 0.1) * S" :y="(area.y + 0.3) * S">{{ t('table.elevation', { level: area.level }) }}</text>
      </g>

      <g v-for="zone in layout.zones" :key="zone.key" class="zone">
        <rect :x="zone.x * S + 4" :y="zone.y * S + 4" :width="zone.w * S - 8" :height="zone.h * S - 8" />
        <text
          :x="(zone.x + zone.w / 2) * S"
          :y="(zone.y + zone.h / 2) * S"
          :transform="
            zone.h > zone.w ? `rotate(-90 ${(zone.x + zone.w / 2) * S} ${(zone.y + zone.h / 2) * S})` : undefined
          "
        >
          {{ zone.kind === 'startingZone' ? t('table.start') : zone.key }}
        </text>
      </g>

      <g
        v-for="current in layout.currents"
        :key="current.key"
        class="current"
        :transform="`translate(${current.x * S} ${current.y * S}) rotate(${current.angle})`"
      >
        <title>{{ t('table.current', { strength: current.strength }) }}</title>
        <path d="M -120 0 L 80 0 M 40 -40 L 100 0 L 40 40" />
      </g>

      <g
        v-for="token in layout.tokens"
        :key="token.id"
        class="token"
        :class="{ selected: isSelected('token', token.id) }"
        :data-token="token.id"
        role="button"
        tabindex="0"
        :aria-label="t('table.token', { token: token.key })"
        @keydown.enter="select({ kind: 'token', id: token.id })"
      >
        <image
          v-if="token.href"
          :href="token.href"
          :x="(token.x + token.w / 2 - (token.turned ? token.h : token.w) / 2) * S + 4"
          :y="(token.y + token.h / 2 - (token.turned ? token.w : token.h) / 2) * S + 4"
          :width="(token.turned ? token.h : token.w) * S - 8"
          :height="(token.turned ? token.w : token.h) * S - 8"
          preserveAspectRatio="none"
          :clip-path="token.round ? `url(#${clipId})` : undefined"
          :transform="
            token.turned ? `rotate(90 ${(token.x + token.w / 2) * S} ${(token.y + token.h / 2) * S})` : undefined
          "
        />
        <rect
          v-else
          class="missing"
          :x="token.x * S + 8"
          :y="token.y * S + 8"
          :width="token.w * S - 16"
          :height="token.h * S - 16"
        />
        <rect
          v-if="isSelected('token', token.id)"
          class="selection"
          :x="token.x * S"
          :y="token.y * S"
          :width="token.w * S"
          :height="token.h * S"
        />
      </g>

      <g
        v-for="figure in layout.figures"
        :key="figure.id"
        class="figure"
        :class="[
          figure.kind,
          {
            acting: figure.acting,
            aggro: figure.aggro,
            selected: isSelected('figure', figure.id),
            dragged: drag?.id === figure.id,
          },
        ]"
        :data-figure="figure.id"
        :transform="figureTransform(figure)"
        role="button"
        tabindex="0"
        :aria-label="t('table.figure', { name: figure.name })"
        @keydown.enter="select({ kind: 'figure', id: figure.id })"
      >
        <circle
          class="base"
          :cx="(figure.x + figure.size / 2) * S"
          :cy="(figure.y + figure.size / 2) * S"
          :r="figure.size * S * 0.44"
        />
        <image
          v-if="figure.href"
          :href="figure.href"
          :x="(figure.x + figure.size * 0.06) * S"
          :y="(figure.y + figure.size * 0.06) * S"
          :width="figure.size * S * 0.88"
          :height="figure.size * S * 0.88"
          preserveAspectRatio="xMinYMid slice"
          :clip-path="`url(#${clipId})`"
        />
        <circle
          class="ring"
          :cx="(figure.x + figure.size / 2) * S"
          :cy="(figure.y + figure.size / 2) * S"
          :r="figure.size * S * 0.44"
        />
        <circle
          class="badge"
          :cx="(figure.x + figure.size * 0.78) * S"
          :cy="(figure.y + figure.size * 0.78) * S"
          r="18"
        />
        <text class="badge-text" :x="(figure.x + figure.size * 0.78) * S" :y="(figure.y + figure.size * 0.78) * S">
          {{ figure.label }}
        </text>
      </g>
    </svg>
  </div>
</template>

<style scoped>
.canvas {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
  min-height: 0;
  height: 100%;
}
svg {
  display: block;
  flex: 1;
  min-height: 0;
  width: 100%;
  background: #1b1b1b;
  border-radius: var(--p-content-border-radius);
  touch-action: none;
  user-select: none;
}
.tools {
  display: flex;
  justify-content: flex-end;
  gap: 0.3rem;
}
.tile-edge {
  fill: none;
  stroke: rgb(0 0 0 / 0.6);
  stroke-width: 3;
  pointer-events: none;
}
.missing {
  fill: #333;
  stroke: #666;
  stroke-dasharray: 10 6;
}
.fog {
  cursor: pointer;
}
.fog text {
  fill: #6b7280;
  font-size: 160px;
  font-weight: 700;
  text-anchor: middle;
  dominant-baseline: central;
}
.fog.selected rect,
.fog:focus-visible rect {
  stroke: #fbbf24;
  stroke-width: 10;
}
.elevation,
.zone,
.current {
  pointer-events: none;
}
.elevation rect {
  fill: rgb(255 255 255 / 0.06);
  stroke: #fff;
  stroke-dasharray: 12 8;
  stroke-width: 3;
}
.elevation text {
  fill: #fff;
  font-size: 22px;
}
.zone rect {
  fill: rgb(56 189 248 / 0.18);
  stroke: #38bdf8;
  stroke-width: 6;
}
.zone text {
  fill: #fff;
  font-size: 34px;
  font-weight: 700;
  letter-spacing: 0.1em;
  text-anchor: middle;
  dominant-baseline: middle;
  text-transform: uppercase;
  paint-order: stroke;
  stroke: rgb(0 0 0 / 0.7);
  stroke-width: 6px;
}
.current path {
  fill: none;
  stroke: #bae6fd;
  stroke-width: 10;
  stroke-linecap: round;
  stroke-linejoin: round;
  opacity: 0.85;
}
.token,
.figure {
  cursor: pointer;
}
.token:hover image,
.token:focus-visible image {
  filter: brightness(1.25);
}
.token .selection {
  fill: none;
  stroke: #fbbf24;
  stroke-width: 8;
}
.token:focus-visible,
.figure:focus-visible,
.fog:focus-visible {
  outline: none;
}
.figure .base {
  fill: #e5e5e5;
}
.figure .ring {
  fill: none;
  stroke: #dc2626;
  stroke-width: 7;
}
.figure.adventurer .ring {
  stroke: #2563eb;
}
.figure.aggro .ring {
  stroke-dasharray: 14 8;
}
.figure.acting .ring {
  stroke: #f59e0b;
  stroke-width: 12;
  animation: pulse 1.2s ease-in-out infinite;
}
.figure.selected .ring,
.figure:focus-visible .ring {
  stroke: #fbbf24;
  stroke-width: 14;
}
.figure.dragged {
  opacity: 0.8;
  cursor: grabbing;
}
.badge {
  fill: #111;
  stroke: #fff;
  stroke-width: 3;
}
.badge-text {
  fill: #fff;
  font-size: 22px;
  font-weight: 700;
  text-anchor: middle;
  dominant-baseline: central;
  pointer-events: none;
}
@keyframes pulse {
  50% {
    stroke-opacity: 0.4;
  }
}
@media (prefers-reduced-motion: reduce) {
  .figure.acting .ring {
    animation: none;
  }
}
</style>
