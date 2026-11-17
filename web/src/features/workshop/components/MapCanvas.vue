<script setup lang="ts">
import { computed, ref, useId, useTemplateRef } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import {
  cellAt,
  figureArt,
  fitBox,
  panBox,
  tokenImage,
  zoomBox,
  type BoardPieces,
  type ViewBox,
} from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import {
  extentOf,
  overlappingTiles,
  rectOf,
  sameItem,
  spanOf,
  START_ZONE,
  TILE,
  tokenRect,
  type DraftMap,
  type DraftSetup,
  type MapItem,
  type Rect,
} from '../lib/map'

/** What the next tap on the board lays down. */
export type MapTool =
  | { kind: 'tile'; ref: string }
  | { kind: 'token'; type: string; color?: string; size?: string; image?: string }
  | { kind: 'combatant'; family: string }
  | { kind: 'zone'; zoneKind: string }

/**
 * The map editor's board: the tiles, tokens, zones and setup combatants of an encounter on a grid.
 * Drag an item to move it (it snaps to the squares), tap to select it; with a tool, tap a square to
 * lay a piece there (a zone is drawn by dragging). Drag the background to pan, wheel to zoom.
 */
const props = defineProps<{
  map: DraftMap
  setup: DraftSetup
  pieces: BoardPieces | undefined
  selected: MapItem | null
  tool: MapTool | null
}>()
const emit = defineEmits<{
  select: [item: MapItem | null]
  move: [item: MapItem, to: { x: number; y: number }]
  place: [cell: { x: number; y: number }]
  draw: [rect: Rect]
}>()

const S = 100
const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const svg = useTemplateRef<SVGSVGElement>('svg')
const clipId = `map-clip-${useId()}`
const fogId = `map-fog-${useId()}`

const board = computed(() => ({ map: props.map, setup: props.setup }))
/** The board and one tile's worth around it, to lay new tiles. */
const grid = computed(() => {
  const { width, height } = extentOf(board.value)
  return { width: width + TILE, height: height + TILE }
})
const full = computed(() => fitBox(grid.value.width * S, grid.value.height * S, S / 2))
const box = ref<ViewBox>(full.value)
const overlaps = computed(() => overlappingTiles(props.map))

const sizeOfFamily = (family: string | undefined) =>
  family && (props.pieces?.figures[family]?.baseSize ?? props.pieces?.families[family]?.baseSize) === '2x2' ? 2 : 1

const tiles = computed(() =>
  props.map.tiles.map((tile, index) => {
    const piece = props.pieces?.tiles[tile.ref]
    return {
      index,
      tile,
      label: piece?.name ?? tile.ref.split('/').pop() ?? tile.ref,
      href: assetUrl(piece?.image),
      area: tile.area ? props.map.areas.find((a) => a.id === tile.area) : undefined,
    }
  }),
)
const tokens = computed(() =>
  props.map.tokens.map((token) => {
    const piece = props.pieces?.tokens[token.type]
    const r = tokenRect(token)
    return {
      token,
      r,
      href: assetUrl(tokenImage(token, piece)),
      round: !!piece && piece.category !== 'terrain',
      turned: r.h > r.w,
      label: `${pick(piece?.name, language.value) || token.type}${token.color ? ` (${token.color})` : ''}`,
    }
  }),
)
const combatants = computed(() =>
  props.setup.combatants.flatMap((c, index) => {
    if (!c.at) return []
    const family =
      c.family ??
      c.ref
        ?.split('/')
        .pop()
        ?.replace(/-g\d.*$/, '')
    const size = sizeOfFamily(family)
    return [
      {
        index,
        x: c.at.x,
        y: c.at.y,
        size,
        ...(({ image, portrait }) => ({ href: assetUrl(image), portrait }))(figureArt(props.pieces, family)),
        name: (family && pick(props.pieces?.families[family]?.name, language.value)) || family || c.ref || '?',
      },
    ]
  }),
)

function toSvg(event: { clientX: number; clientY: number }) {
  const ctm = svg.value?.getScreenCTM()
  if (!ctm) return { x: 0, y: 0 }
  const point = new DOMPoint(event.clientX, event.clientY).matrixTransform(ctm.inverse())
  return { x: point.x, y: point.y }
}
const clamp = (cell: { x: number; y: number }) => ({ x: Math.max(0, cell.x), y: Math.max(0, cell.y) })

function zoom(factor: number, center = { x: box.value.x + box.value.w / 2, y: box.value.y + box.value.h / 2 }) {
  box.value = zoomBox(box.value, factor, center, full.value)
}

interface Gesture {
  pointerId: number
  clientX: number
  clientY: number
  scale: number
  startBox: ViewBox
  item: MapItem | null
  /** The square grabbed, relative to the item's top-left one. */
  grab: { x: number; y: number }
  start: { x: number; y: number }
  moved: boolean
}
let gesture: Gesture | null = null
/** The item being dragged: where its top-left square would land. */
const dragging = ref<{ item: MapItem; to: { x: number; y: number } } | null>(null)
/** The zone being drawn. */
const drawing = ref<Rect | null>(null)
/** The square under the pointer, to show where the tool lays its piece. */
const hover = ref<{ x: number; y: number } | null>(null)

function itemOf(element: EventTarget | null): MapItem | null {
  const found = (element as Element | null)?.closest?.('[data-item]')
  if (!found) return null
  const [kind, key] = (found.getAttribute('data-item') ?? '').split('|')
  if (kind === 'tile' || kind === 'combatant') return { kind, index: Number(key) }
  if (kind === 'token' || kind === 'zone') return { kind, id: key ?? '' }
  return null
}

function onPointerDown(event: PointerEvent) {
  if (event.pointerType === 'mouse' && event.button !== 0) return
  svg.value?.setPointerCapture?.(event.pointerId)
  const cell = clamp(cellAt(toSvg(event), S))
  // With a tool, the board lays pieces: items under the pointer are not grabbed.
  const item = props.tool ? null : itemOf(event.target)
  const r = item ? rectOf(board.value, item) : undefined
  gesture = {
    pointerId: event.pointerId,
    clientX: event.clientX,
    clientY: event.clientY,
    scale: svg.value?.getScreenCTM()?.a ?? 1,
    startBox: box.value,
    item,
    grab: r ? { x: cell.x - r.x, y: cell.y - r.y } : { x: 0, y: 0 },
    start: cell,
    moved: false,
  }
  if (props.tool?.kind === 'zone') drawing.value = spanOf(cell, cell)
}

function onPointerMove(event: PointerEvent) {
  const cell = clamp(cellAt(toSvg(event), S))
  hover.value = props.tool ? cell : null
  if (!gesture || gesture.pointerId !== event.pointerId) return
  const dx = event.clientX - gesture.clientX
  const dy = event.clientY - gesture.clientY
  if (!gesture.moved && Math.hypot(dx, dy) < 6) return
  gesture.moved = true
  if (props.tool?.kind === 'zone') drawing.value = spanOf(gesture.start, cell)
  else if (gesture.item)
    dragging.value = { item: gesture.item, to: clamp({ x: cell.x - gesture.grab.x, y: cell.y - gesture.grab.y }) }
  else box.value = panBox(gesture.startBox, dx / gesture.scale, dy / gesture.scale)
}

function onPointerUp(event: PointerEvent) {
  if (!gesture || gesture.pointerId !== event.pointerId) return
  const { item, moved, start } = gesture
  gesture = null
  if (props.tool?.kind === 'zone' && drawing.value) emit('draw', drawing.value)
  else if (item && moved && dragging.value) {
    emit('move', item, dragging.value.to)
    emit('select', item)
  } else if (!moved) {
    if (props.tool) emit('place', start)
    else emit('select', item)
  }
  dragging.value = null
  drawing.value = null
}

function onPointerCancel() {
  gesture = null
  dragging.value = null
  drawing.value = null
}

function shift(item: MapItem, x: number, y: number): string | undefined {
  if (!dragging.value || !sameItem(dragging.value.item, item)) return undefined
  return `translate(${(dragging.value.to.x - x) * S} ${(dragging.value.to.y - y) * S})`
}
const isSelected = (item: MapItem) => sameItem(props.selected, item)

/** The outline of what the tool would lay at the hovered square. */
const ghost = computed(() => {
  const tool = props.tool
  const at = hover.value
  if (!tool || !at || tool.kind === 'zone') return null
  if (tool.kind === 'tile') return { ...at, w: TILE, h: TILE }
  if (tool.kind === 'combatant') {
    const size = sizeOfFamily(tool.family)
    return { ...at, w: size, h: size }
  }
  const match = /^(\d+)x(\d+)$/.exec(tool.size ?? '')
  return { ...at, w: match ? Number(match[1]) : 1, h: match ? Number(match[2]) : 1 }
})

const zoneLabel = (kind: string | undefined, id: string) => (kind === START_ZONE ? t('table.start') : id)
</script>

<template>
  <div class="canvas">
    <div class="tools">
      <slot name="tools" />
      <span class="spacer" />
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
        icon="pi pi-refresh"
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
      role="application"
      :aria-label="t('workshop.map.board')"
      :class="{ placing: !!tool }"
      @wheel.prevent="(e) => zoom(e.deltaY < 0 ? 1.2 : 1 / 1.2, toSvg(e))"
      @pointerdown="onPointerDown"
      @pointermove="onPointerMove"
      @pointerup="onPointerUp"
      @pointercancel="onPointerCancel"
      @pointerleave="hover = null"
    >
      <defs>
        <clipPath :id="clipId" clipPathUnits="objectBoundingBox">
          <circle cx="0.5" cy="0.5" r="0.5" />
        </clipPath>
        <pattern :id="fogId" width="40" height="40" patternUnits="userSpaceOnUse" patternTransform="rotate(35)">
          <rect width="40" height="40" fill="rgb(20 22 26 / 0.45)" />
          <rect width="20" height="40" fill="rgb(20 22 26 / 0)" />
        </pattern>
      </defs>

      <!-- The grid -->
      <g class="grid" aria-hidden="true">
        <rect :width="grid.width * S" :height="grid.height * S" />
        <line
          v-for="x in grid.width + 1"
          :key="`x${x}`"
          :x1="(x - 1) * S"
          :x2="(x - 1) * S"
          y1="0"
          :y2="grid.height * S"
        />
        <line
          v-for="y in grid.height + 1"
          :key="`y${y}`"
          :y1="(y - 1) * S"
          :y2="(y - 1) * S"
          x1="0"
          :x2="grid.width * S"
        />
      </g>

      <g
        v-for="{ index, tile, label, href, area } in tiles"
        :key="`tile-${index}`"
        class="tile"
        :class="{ selected: isSelected({ kind: 'tile', index }), overlap: overlaps.has(index) }"
        :data-item="`tile|${index}`"
        :transform="shift({ kind: 'tile', index }, tile.x, tile.y)"
        role="button"
        tabindex="0"
        :aria-label="t('workshop.map.tileItem', { name: label, x: tile.x, y: tile.y })"
        @keydown.enter="emit('select', { kind: 'tile', index })"
      >
        <image
          v-if="href"
          :href="href"
          :x="tile.x * S"
          :y="tile.y * S"
          :width="TILE * S"
          :height="TILE * S"
          preserveAspectRatio="none"
          :transform="`rotate(${tile.rotation ?? 0} ${(tile.x + TILE / 2) * S} ${(tile.y + TILE / 2) * S})`"
        />
        <rect v-else class="missing" :x="tile.x * S" :y="tile.y * S" :width="TILE * S" :height="TILE * S" />
        <rect
          v-if="area"
          class="fogged"
          :x="tile.x * S"
          :y="tile.y * S"
          :width="TILE * S"
          :height="TILE * S"
          :fill="`url(#${fogId})`"
        />
        <text class="tile-name" :x="tile.x * S + 14" :y="tile.y * S + 40">
          {{ label }}{{ area ? ` · ${pick(area.name, language) || area.id}` : '' }}
        </text>
        <rect class="edge" :x="tile.x * S" :y="tile.y * S" :width="TILE * S" :height="TILE * S" />
      </g>

      <g
        v-for="zone in map.zones"
        :key="`zone-${zone.id}`"
        class="zone"
        :class="[zone.kind, { selected: isSelected({ kind: 'zone', id: zone.id }) }]"
        :data-item="`zone|${zone.id}`"
        :transform="shift({ kind: 'zone', id: zone.id }, zone.rect.x, zone.rect.y)"
        role="button"
        tabindex="0"
        :aria-label="t('workshop.map.zoneItem', { name: zoneLabel(zone.kind, zone.id) })"
        @keydown.enter="emit('select', { kind: 'zone', id: zone.id })"
      >
        <rect
          :x="zone.rect.x * S + 4"
          :y="zone.rect.y * S + 4"
          :width="zone.rect.w * S - 8"
          :height="zone.rect.h * S - 8"
        />
        <text :x="(zone.rect.x + zone.rect.w / 2) * S" :y="(zone.rect.y + zone.rect.h / 2) * S">
          {{ zoneLabel(zone.kind, zone.id) }}
        </text>
      </g>

      <g
        v-for="{ token, r, href, round, turned, label } in tokens"
        :key="`token-${token.id}`"
        class="token"
        :class="{ selected: isSelected({ kind: 'token', id: token.id }) }"
        :data-item="`token|${token.id}`"
        :transform="shift({ kind: 'token', id: token.id }, r.x, r.y)"
        role="button"
        tabindex="0"
        :aria-label="t('workshop.map.tokenItem', { name: label, x: r.x, y: r.y })"
        @keydown.enter="emit('select', { kind: 'token', id: token.id })"
      >
        <image
          v-if="href"
          :href="href"
          :x="(r.x + r.w / 2 - (turned ? r.h : r.w) / 2) * S + 4"
          :y="(r.y + r.h / 2 - (turned ? r.w : r.h) / 2) * S + 4"
          :width="(turned ? r.h : r.w) * S - 8"
          :height="(turned ? r.w : r.h) * S - 8"
          preserveAspectRatio="none"
          :clip-path="round ? `url(#${clipId})` : undefined"
          :transform="turned ? `rotate(90 ${(r.x + r.w / 2) * S} ${(r.y + r.h / 2) * S})` : undefined"
        />
        <rect v-else class="missing" :x="r.x * S + 8" :y="r.y * S + 8" :width="r.w * S - 16" :height="r.h * S - 16" />
        <rect class="outline" :x="r.x * S + 2" :y="r.y * S + 2" :width="r.w * S - 4" :height="r.h * S - 4" />
      </g>

      <g
        v-for="c in combatants"
        :key="`combatant-${c.index}`"
        class="figure"
        :class="{ selected: isSelected({ kind: 'combatant', index: c.index }) }"
        :data-item="`combatant|${c.index}`"
        :transform="shift({ kind: 'combatant', index: c.index }, c.x, c.y)"
        role="button"
        tabindex="0"
        :aria-label="t('workshop.map.combatantItem', { name: c.name, x: c.x, y: c.y })"
        @keydown.enter="emit('select', { kind: 'combatant', index: c.index })"
      >
        <circle class="base" :cx="(c.x + c.size / 2) * S" :cy="(c.y + c.size / 2) * S" :r="c.size * S * 0.44" />
        <image
          v-if="c.href"
          :href="c.href"
          :x="(c.x + c.size * 0.06) * S"
          :y="(c.y + c.size * 0.06) * S"
          :width="c.size * S * 0.88"
          :height="c.size * S * 0.88"
          :preserveAspectRatio="c.portrait ? 'xMidYMid slice' : 'xMinYMid slice'"
          :clip-path="`url(#${clipId})`"
        />
        <text v-else class="initial" :x="(c.x + c.size / 2) * S" :y="(c.y + c.size / 2) * S">
          {{ c.name.slice(0, 2) }}
        </text>
        <circle class="ring" :cx="(c.x + c.size / 2) * S" :cy="(c.y + c.size / 2) * S" :r="c.size * S * 0.44" />
      </g>

      <rect
        v-if="ghost"
        class="ghost"
        :x="ghost.x * S"
        :y="ghost.y * S"
        :width="ghost.w * S"
        :height="ghost.h * S"
        aria-hidden="true"
      />
      <rect
        v-if="drawing"
        class="ghost"
        :x="drawing.x * S"
        :y="drawing.y * S"
        :width="drawing.w * S"
        :height="drawing.h * S"
        aria-hidden="true"
      />
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
.tools {
  display: flex;
  align-items: center;
  gap: 0.3rem;
}
.spacer {
  flex: 1;
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
svg.placing {
  cursor: crosshair;
}
.grid rect {
  fill: #242424;
}
.grid line {
  stroke: rgb(255 255 255 / 0.08);
  stroke-width: 2;
}
.tile,
.token,
.figure,
.zone {
  cursor: grab;
}
svg.placing .tile,
svg.placing .token,
svg.placing .figure,
svg.placing .zone {
  cursor: crosshair;
}
.tile .edge {
  fill: none;
  stroke: rgb(0 0 0 / 0.6);
  stroke-width: 3;
}
.tile.overlap .edge {
  stroke: #ef4444;
  stroke-width: 10;
  stroke-dasharray: 20 10;
}
.tile.selected .edge,
.tile:focus-visible .edge {
  stroke: #fbbf24;
  stroke-width: 10;
}
.tile-name {
  fill: #fff;
  font-size: 30px;
  font-weight: 700;
  paint-order: stroke;
  stroke: rgb(0 0 0 / 0.8);
  stroke-width: 6px;
  pointer-events: none;
}
.fogged {
  pointer-events: none;
}
.missing {
  fill: #333;
  stroke: #666;
  stroke-dasharray: 10 6;
}
.zone rect {
  fill: rgb(56 189 248 / 0.18);
  stroke: #38bdf8;
  stroke-width: 6;
}
.zone.spawn rect {
  fill: rgb(244 63 94 / 0.16);
  stroke: #f43f5e;
}
.zone.selected rect,
.zone:focus-visible rect {
  stroke: #fbbf24;
  stroke-width: 10;
}
.zone text {
  fill: #fff;
  font-size: 30px;
  font-weight: 700;
  text-anchor: middle;
  dominant-baseline: middle;
  text-transform: uppercase;
  paint-order: stroke;
  stroke: rgb(0 0 0 / 0.7);
  stroke-width: 6px;
  pointer-events: none;
}
.token .outline {
  fill: none;
  stroke: transparent;
  stroke-width: 6;
}
.token.selected .outline,
.token:focus-visible .outline {
  stroke: #fbbf24;
}
.figure .base {
  fill: #e5e5e5;
}
.figure .ring {
  fill: none;
  stroke: #dc2626;
  stroke-width: 7;
}
.figure.selected .ring,
.figure:focus-visible .ring {
  stroke: #fbbf24;
  stroke-width: 14;
}
.figure .initial {
  fill: #111;
  font-size: 34px;
  font-weight: 700;
  text-anchor: middle;
  dominant-baseline: central;
  text-transform: uppercase;
}
.ghost {
  fill: rgb(251 191 36 / 0.2);
  stroke: #fbbf24;
  stroke-width: 6;
  stroke-dasharray: 14 8;
  pointer-events: none;
}
.tile:focus-visible,
.token:focus-visible,
.figure:focus-visible,
.zone:focus-visible {
  outline: none;
}
</style>
