<script setup lang="ts">
import { computed, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { figureArt, tokenImage, type BoardPieces } from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import type { DraftNode } from '../api/types'
import { extentOf, mapOf, setupOf, START_ZONE, TILE, tokenRect, type DraftMap, type DraftSetup } from '../lib/map'
import { terrainFrame, TOKEN_COLORS } from '../lib/printable'

/**
 * The diagram of an encounter for print: tiles, terrain, tokens, figures and the starting zone,
 * drawn as the books do: each terrain token framed in the colour of its kind, each coloured token
 * ringed in its colour. A piece without a picture (BE1's tokens, cut out of no sheet yet) is drawn
 * all the same: its frame or its colour, and its name.
 * `columns` shows only some columns of squares (one half of a large map, printed across two pages):
 * the black frame is then left open on the side of the cut, so the two halves join. `span` is the
 * width given to every half, so that both print at the same scale; each half sits against the cut.
 */
const props = defineProps<{
  node: DraftNode
  pieces: BoardPieces | undefined
  columns?: { from: number; to: number; span: number }
  /**
   * A diagram of the encounter (what a hidden text lays during the fight): drawn bright over the
   * encounter's board, faded as the books print it (tiles, terrain, tokens and zones, no figures).
   */
  diagram?: { map: DraftMap; setup: DraftSetup }
  /** The tiles the encounter's earlier diagrams added (the board this diagram builds on), faded too. */
  under?: DraftMap[]
}>()

const S = 100
/** The black frame of the board around each tile and each terrain token laid off the tiles. */
const BORDER = 10
const { t } = useI18n()
const clipId = `print-clip-${useId()}`
const map = computed(() => mapOf(props.node))
const setup = computed(() => setupOf(props.node))
const NONE: DraftSetup = { combatants: [] }
/** The board in layers: the encounter's (faded under a diagram), the earlier tiles, then the diagram. */
const layers = computed(() =>
  props.diagram
    ? [
        { key: 'base', faded: true, map: map.value, setup: NONE },
        // Their tiles and terrain: the tokens they laid have been taken or flipped since
        ...(props.under ?? []).map((m, i) => ({
          key: `under-${i}`,
          faded: true,
          terrainOnly: true,
          map: { ...m, zones: [] },
          setup: NONE,
        })),
        { key: 'diagram', faded: false, map: props.diagram.map, setup: props.diagram.setup },
      ]
    : [{ key: 'base', faded: false, map: map.value, setup: setup.value }],
)
const size = computed(() =>
  extentOf({
    map: {
      ...map.value,
      tiles: layers.value.flatMap((l) => l.map.tiles),
      tokens: layers.value.flatMap((l) => l.map.tokens),
      zones: layers.value.flatMap((l) => l.map.zones),
    },
    setup: { combatants: layers.value.flatMap((l) => l.setup.combatants) },
  }),
)
const PAD = 30
/** The part shown: x and w of the squares drawn, and of the whole box (as wide as the widest half). */
const frame = computed(() => {
  const from = props.columns?.from ?? size.value.x
  const to = props.columns?.to ?? size.value.width
  const left = from > size.value.x ? 0 : PAD
  const right = to < size.value.width ? 0 : PAD
  const shown = { x: from * S - left, w: (to - from) * S + left + right }
  const box = props.columns ? props.columns.span * S + PAD : shown.w
  // The left half sits at the right of its box, against the cut; the right half at the left
  const x = props.columns && from === size.value.x ? shown.x - (box - shown.w) : shown.x
  return { shown, box: { x, w: box } }
})
const viewBox = computed(() => ({
  x: frame.value.box.x,
  y: size.value.y * S - PAD,
  w: frame.value.box.w,
  h: (size.value.height - size.value.y) * S + 2 * PAD,
}))

const nameOf = (name: { en?: string } | Record<string, string> | undefined) =>
  (name as Record<string, string> | undefined)?.en ?? Object.values(name ?? {})[0] ?? ''
/** A name's font size, so that it holds within `squares` squares (at most 15 px). */
const fit = (label: string, squares: number) => Math.min(15, (squares * S * 0.8) / Math.max(1, label.length * 0.55))

function drawn(layer: { key: string; faded: boolean; terrainOnly?: boolean; map: DraftMap; setup: DraftSetup }) {
  const tiles = layer.map.tiles.map((tile, i) => ({
    key: `${tile.ref}-${i}`,
    tile,
    href: assetUrl(props.pieces?.tiles[tile.ref]?.image),
  }))
  // Terrain first, then the round tokens on top (a totem on a Bug Creep stays visible)
  const tokens = layer.map.tokens
    .map((token) => {
      const piece = props.pieces?.tokens[token.type]
      const r = tokenRect(token)
      const round = !!piece && piece.category !== 'terrain'
      return {
        id: token.id,
        r,
        turned: r.h > r.w,
        round,
        href: assetUrl(tokenImage(token, piece)),
        frame: round ? undefined : terrainFrame(token.type),
        ring: (token.color && TOKEN_COLORS[token.color]) || '#f5f5f5',
        label: nameOf(piece?.name).replace(/\s+\d+\s*[xX]\s*\d+$/, ''),
      }
    })
    .filter((tk) => !layer.terrainOnly || !tk.round)
    .sort((a, b) => Number(a.round) - Number(b.round))
  // The board's black ground: under each tile, and under the terrain laid off them (a passage)
  const ground = [
    ...layer.map.tiles.map((tile) => ({ x: tile.x, y: tile.y, w: TILE, h: TILE })),
    ...tokens.filter((tk) => !tk.round).map((tk) => tk.r),
  ]
  const figures = layer.setup.combatants.flatMap((c, i) => {
    if (!c.at || !c.family) return []
    const n =
      (props.pieces?.figures[c.family]?.baseSize ?? props.pieces?.families[c.family]?.baseSize) === '2x2' ? 2 : 1
    // The round portrait of the books' diagrams, else the standee
    const art = figureArt(props.pieces, c.family)
    const label = nameOf(props.pieces?.families[c.family]?.name) || c.family
    return [{ key: i, x: c.at.x, y: c.at.y, n, href: assetUrl(art.image), portrait: art.portrait, label }]
  })
  return { key: layer.key, faded: layer.faded, ground, tiles, zones: layer.map.zones, tokens, figures }
}
const drawing = computed(() => layers.value.map(drawn))
</script>

<template>
  <svg
    class="print-map"
    :viewBox="`${viewBox.x} ${viewBox.y} ${viewBox.w} ${viewBox.h}`"
    :preserveAspectRatio="columns ? (columns.from === size.x ? 'xMaxYMid meet' : 'xMinYMid meet') : 'xMidYMid meet'"
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
      <g v-for="layer in drawing" :key="layer.key" :class="{ faded: layer.faded }">
        <rect
          v-for="(r, i) in layer.ground"
          :key="`ground-${i}`"
          class="ground"
          :x="r.x * S - BORDER"
          :y="r.y * S - BORDER"
          :width="r.w * S + 2 * BORDER"
          :height="r.h * S + 2 * BORDER"
        />
        <g v-for="{ key, tile, href } in layer.tiles" :key="key">
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
        <g v-for="zone in layer.zones" :key="zone.id" :class="['zone', zone.kind]">
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
        <g v-for="token in layer.tokens" :key="token.id">
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
          <!-- No picture: the colour of a round token, a dark square for terrain, and the name -->
          <circle
            v-if="!token.href && token.round"
            class="blank"
            :cx="(token.r.x + token.r.w / 2) * S"
            :cy="(token.r.y + token.r.h / 2) * S"
            :r="token.r.w * S * 0.44"
            :style="{ fill: token.ring }"
          />
          <rect
            v-if="!token.href && !token.round"
            class="blank"
            :x="token.r.x * S + 6"
            :y="token.r.y * S + 6"
            :width="token.r.w * S - 12"
            :height="token.r.h * S - 12"
            :style="{ fill: token.frame ?? '#555' }"
          />
          <!-- A framed terrain is told by its frame (and the setup list); a round token by its name -->
          <text
            v-if="!token.href && !token.frame"
            class="name"
            :x="(token.r.x + token.r.w / 2) * S"
            :y="(token.r.y + token.r.h / 2) * S"
            :style="{ fontSize: `${fit(token.label, token.r.w)}px` }"
          >
            {{ token.label }}
          </text>
          <rect
            v-if="token.frame"
            class="frame"
            :x="token.r.x * S + 5"
            :y="token.r.y * S + 5"
            :width="token.r.w * S - 10"
            :height="token.r.h * S - 10"
            :style="{ stroke: token.frame }"
          />
          <circle
            v-if="token.round"
            class="token-ring"
            :cx="(token.r.x + token.r.w / 2) * S"
            :cy="(token.r.y + token.r.h / 2) * S"
            :r="token.r.w * S * 0.44"
            :style="{ stroke: token.ring }"
          />
        </g>
        <g v-for="f in layer.figures" :key="f.key">
          <circle class="base" :cx="(f.x + f.n / 2) * S" :cy="(f.y + f.n / 2) * S" :r="f.n * S * 0.44" />
          <image
            v-if="f.href"
            :href="f.href"
            :x="(f.x + f.n * 0.06) * S"
            :y="(f.y + f.n * 0.06) * S"
            :width="f.n * S * 0.88"
            :height="f.n * S * 0.88"
            :preserveAspectRatio="f.portrait ? 'xMidYMid slice' : 'xMinYMid slice'"
            :clip-path="`url(#${clipId})`"
          />
          <text
            v-if="!f.href"
            class="name"
            :x="(f.x + f.n / 2) * S"
            :y="(f.y + f.n / 2) * S"
            :style="{ fontSize: `${fit(f.label, f.n)}px` }"
          >
            {{ f.label }}
          </text>
          <circle class="ring" :cx="(f.x + f.n / 2) * S" :cy="(f.y + f.n / 2) * S" :r="f.n * S * 0.44" />
        </g>
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
/* The board's black edge; the page shows around it (an L-shaped board, a passage between tiles) */
.ground {
  fill: #0b0b0b;
}
/* What is already on the table under a diagram, as the books print it: faded */
.faded {
  opacity: 0.5;
  filter: grayscale(0.8) sepia(0.35);
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
/* The starting zone as the books draw it: darkened, framed in cyan */
.zone.startingZone rect {
  fill: rgb(0 0 0 / 0.55);
  stroke: #3fb6d8;
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
  stroke-width: 7;
}
/* The frame of a terrain token, as the books draw it */
.frame {
  fill: none;
  stroke-width: 8;
  rx: 6;
}
.blank {
  fill-opacity: 0.3;
}
.name {
  fill: #fff;
  font-family: var(--print-body-font);
  font-size: 15px;
  font-weight: 700;
  text-anchor: middle;
  dominant-baseline: middle;
  paint-order: stroke;
  stroke: #000;
  stroke-width: 4px;
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
