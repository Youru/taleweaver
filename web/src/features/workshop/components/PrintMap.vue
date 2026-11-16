<script setup lang="ts">
import { computed, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import type { BoardPieces } from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import type { DraftNode } from '../api/types'
import { extentOf, mapOf, setupOf, START_ZONE, TILE, tokenRect } from '../lib/map'

/**
 * The diagram of an encounter for print: tiles, terrain, tokens, figures and the starting zone.
 * `columns` shows only some columns of squares (one half of a large map, printed across two pages):
 * the black frame is then left open on the side of the cut, so the two halves join. `span` is the
 * width given to every half, so that both print at the same scale; each half sits against the cut.
 */
const props = defineProps<{
  node: DraftNode
  pieces: BoardPieces | undefined
  columns?: { from: number; to: number; span: number }
}>()

const S = 100
const { t } = useI18n()
const clipId = `print-clip-${useId()}`
const map = computed(() => mapOf(props.node))
const setup = computed(() => setupOf(props.node))
const size = computed(() => extentOf({ map: map.value, setup: setup.value }))
const PAD = 30
/** The part shown: x and w of the squares drawn, and of the whole box (as wide as the widest half). */
const frame = computed(() => {
  const from = props.columns?.from ?? 0
  const to = props.columns?.to ?? size.value.width
  const left = from > 0 ? 0 : PAD
  const right = to < size.value.width ? 0 : PAD
  const shown = { x: from * S - left, w: (to - from) * S + left + right }
  const box = props.columns ? props.columns.span * S + PAD : shown.w
  // The left half sits at the right of its box, against the cut; the right half at the left
  const x = props.columns && from === 0 ? shown.x - (box - shown.w) : shown.x
  return { shown, box: { x, w: box } }
})
const viewBox = computed(() => ({
  x: frame.value.box.x,
  y: -PAD,
  w: frame.value.box.w,
  h: size.value.height * S + 2 * PAD,
}))

const tiles = computed(() =>
  map.value.tiles.map((tile, i) => ({
    key: `${tile.ref}-${i}`,
    tile,
    href: assetUrl(props.pieces?.tiles[tile.ref]?.image),
  })),
)
const tokens = computed(() =>
  map.value.tokens.map((token) => {
    const piece = props.pieces?.tokens[token.type]
    const r = tokenRect(token)
    return {
      id: token.id,
      r,
      turned: r.h > r.w,
      round: !!piece && piece.category !== 'terrain',
      href: assetUrl((token.color && piece?.colors?.[token.color]) || piece?.image),
    }
  }),
)
const figures = computed(() =>
  setup.value.combatants.flatMap((c, i) => {
    if (!c.at || !c.family) return []
    const n =
      (props.pieces?.figures[c.family]?.baseSize ?? props.pieces?.families[c.family]?.baseSize) === '2x2' ? 2 : 1
    return [{ key: i, x: c.at.x, y: c.at.y, n, href: assetUrl(props.pieces?.figures[c.family]?.image) }]
  }),
)
</script>

<template>
  <svg
    class="print-map"
    :viewBox="`${viewBox.x} ${viewBox.y} ${viewBox.w} ${viewBox.h}`"
    :preserveAspectRatio="columns ? (columns.from === 0 ? 'xMaxYMid meet' : 'xMinYMid meet') : 'xMidYMid meet'"
    role="img"
    :aria-label="t('workshop.print.map')"
  >
    <defs>
      <clipPath :id="clipId" clipPathUnits="objectBoundingBox">
        <circle cx="0.5" cy="0.5" r="0.5" />
      </clipPath>
      <clipPath :id="`${clipId}-frame`">
        <rect :x="frame.shown.x" :y="viewBox.y" :width="frame.shown.w" :height="viewBox.h" />
      </clipPath>
    </defs>
    <g :clip-path="`url(#${clipId}-frame)`">
      <rect :x="-PAD" :y="-PAD" :width="size.width * S + 2 * PAD" :height="size.height * S + 2 * PAD" fill="#0b0b0b" />
      <g v-for="{ key, tile, href } in tiles" :key="key">
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
        <rect :x="tile.x * S" :y="tile.y * S" :width="TILE * S" :height="TILE * S" class="edge" />
      </g>
      <g v-for="zone in map.zones" :key="zone.id" :class="['zone', zone.kind]">
        <rect
          :x="zone.rect.x * S + 4"
          :y="zone.rect.y * S + 4"
          :width="zone.rect.w * S - 8"
          :height="zone.rect.h * S - 8"
        />
        <text
          v-if="zone.kind === START_ZONE"
          :x="(zone.rect.x + zone.rect.w / 2) * S"
          :y="(zone.rect.y + zone.rect.h / 2) * S"
          :transform="
            zone.rect.h > zone.rect.w
              ? `rotate(-90 ${(zone.rect.x + zone.rect.w / 2) * S} ${(zone.rect.y + zone.rect.h / 2) * S})`
              : undefined
          "
        >
          {{ t('workshop.print.start') }}
        </text>
      </g>
      <g v-for="token in tokens" :key="token.id">
        <image
          v-if="token.href"
          :href="token.href"
          :x="(token.r.x + token.r.w / 2 - (token.turned ? token.r.h : token.r.w) / 2) * S + 6"
          :y="(token.r.y + token.r.h / 2 - (token.turned ? token.r.w : token.r.h) / 2) * S + 6"
          :width="(token.turned ? token.r.h : token.r.w) * S - 12"
          :height="(token.turned ? token.r.w : token.r.h) * S - 12"
          preserveAspectRatio="none"
          :clip-path="token.round ? `url(#${clipId})` : undefined"
          :transform="
            token.turned
              ? `rotate(90 ${(token.r.x + token.r.w / 2) * S} ${(token.r.y + token.r.h / 2) * S})`
              : undefined
          "
        />
        <circle
          v-if="token.round"
          class="token-ring"
          :cx="(token.r.x + token.r.w / 2) * S"
          :cy="(token.r.y + token.r.h / 2) * S"
          :r="token.r.w * S * 0.44"
        />
      </g>
      <g v-for="f in figures" :key="f.key">
        <circle class="base" :cx="(f.x + f.n / 2) * S" :cy="(f.y + f.n / 2) * S" :r="f.n * S * 0.44" />
        <image
          v-if="f.href"
          :href="f.href"
          :x="(f.x + f.n * 0.06) * S"
          :y="(f.y + f.n * 0.06) * S"
          :width="f.n * S * 0.88"
          :height="f.n * S * 0.88"
          preserveAspectRatio="xMinYMid slice"
          :clip-path="`url(#${clipId})`"
        />
        <circle class="ring" :cx="(f.x + f.n / 2) * S" :cy="(f.y + f.n / 2) * S" :r="f.n * S * 0.44" />
      </g>
    </g>
  </svg>
</template>

<style scoped>
.print-map {
  display: block;
  width: 100%;
  height: 100%;
}
.edge {
  fill: none;
  stroke: #000;
  stroke-width: 6;
}
.zone rect {
  fill: none;
  stroke: #60a5fa;
  stroke-width: 8;
  stroke-dasharray: 18 10;
}
.zone.startingZone rect {
  fill: rgb(191 219 254 / 0.35);
  stroke: #1d9bf0;
  stroke-dasharray: none;
}
.zone text {
  fill: #fff;
  font-family: var(--print-title-font);
  font-size: 40px;
  font-weight: 700;
  text-anchor: middle;
  dominant-baseline: middle;
  font-variant: small-caps;
  paint-order: stroke;
  stroke: #000;
  stroke-width: 6px;
}
.token-ring {
  fill: none;
  stroke: #f5f5f5;
  stroke-width: 6;
}
.base {
  fill: #e5e5e5;
}
.ring {
  fill: none;
  stroke: #b91c1c;
  stroke-width: 8;
}
</style>
