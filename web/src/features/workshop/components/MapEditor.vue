<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Button from 'primevue/button'
import InputNumber from 'primevue/inputnumber'
import InputText from 'primevue/inputtext'
import Select from 'primevue/select'
import SelectButton from 'primevue/selectbutton'
import ToggleSwitch from 'primevue/toggleswitch'
import { figureArt, tokenImage, tokenImages, type BoardPieces } from '@/features/table'
import { assetUrl } from '@/shared/api/assets'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import type { DraftNode } from '../api/types'
import { setText, text } from '../lib/graph'
import {
  addArea,
  addCombatant,
  addTile,
  addToken,
  addZone,
  mapOf,
  moveItem,
  overlappingTiles,
  patchArea,
  patchToken,
  patchZone,
  rectOf,
  removeArea,
  removeItem,
  rotateTile,
  setTileArea,
  setupOf,
  START_ZONE,
  tokenKey,
  turnToken,
  withBoard,
  type DraftMap,
  type DraftSetup,
  type MapItem,
  type Rect,
} from '../lib/map'
import { BOARD_CATEGORIES, tokenChoices, variantLabel } from '../lib/palette'
import MapCanvas, { type MapTool } from './MapCanvas.vue'

/**
 * The map editor of an encounter: a palette (tiles, tokens, enemies, zones and fog), the board, and
 * the inspector of the selected piece. Every change goes back as the whole node.
 */
const props = defineProps<{ node: DraftNode; pieces: BoardPieces | undefined }>()
const emit = defineEmits<{ change: [node: DraftNode]; close: [] }>()

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())

const map = computed(() => mapOf(props.node))
const setup = computed(() => setupOf(props.node))
const board = computed(() => ({ map: map.value, setup: setup.value }))

function commit(change: (board: { map: DraftMap; setup: DraftSetup }) => { map: DraftMap; setup: DraftSetup }) {
  emit('change', withBoard(props.node, change))
}
const changeMap = (change: (map: DraftMap) => DraftMap) => commit((b) => ({ ...b, map: change(b.map) }))

const selected = ref<MapItem | null>(null)
const tool = ref<MapTool | null>(null)
watch(tool, (value) => value && (selected.value = null))

// The palette
type Shelf = 'tiles' | 'tokens' | 'enemies' | 'zones'
const shelf = ref<Shelf>('tiles')
const shelves = computed(() =>
  (['tiles', 'tokens', 'enemies', 'zones'] as const).map((value) => ({ value, label: t(`workshop.map.${value}`) })),
)
const filter = ref('')
const matches = (label: string) => label.toLowerCase().includes(filter.value.trim().toLowerCase())

/** Every tile, by scenery; a tile whose other side is already on the board cannot be laid. */
const tilePieces = computed(() => {
  const laid = new Set(map.value.tiles.map((t) => t.ref))
  const themeOf = (theme: string | undefined) => (theme ? t(`workshop.map.theme.${theme}`, theme) : '')
  return Object.entries(props.pieces?.tiles ?? {})
    .map(([ref, piece]) => ({
      ref,
      name: piece.name,
      theme: themeOf(piece.theme),
      href: assetUrl(piece.image),
      taken: laid.has(ref) || (!!piece.pairedSide && laid.has(piece.pairedSide)),
    }))
    .filter((p) => matches(`${p.name} ${p.theme}`))
    .sort((a, b) => a.theme.localeCompare(b.theme) || a.name.localeCompare(b.name, undefined, { numeric: true }))
})
// Tokens: every type, colour and picture (BE1's cave-style Dangerous…), sorted by category, filtered
// by category, pack and the words searched (a variant is found by its file name: "hole", "eggs")
const category = ref<string | null>(null)
const pack = ref<string | null>(null)
const categoryName = (c: string) => t(`workshop.map.category.${c}`, c)
const tokenPieces = computed(() =>
  tokenChoices(
    props.pieces,
    (type) => pick(props.pieces?.tokens[type]?.name, language.value) || type,
    (color) => t(`workshop.map.color.${color}`, color),
    categoryName,
    { search: filter.value, category: category.value ?? undefined, pack: pack.value ?? undefined },
  ),
)
const categoryOptions = computed(() => BOARD_CATEGORIES.map((value) => ({ value, label: categoryName(value) })))
const packOptions = computed(() =>
  [
    ...new Set(
      tokenChoices(
        props.pieces,
        (x) => x,
        (x) => x,
        (x) => x,
      ).map((c) => c.pack),
    ),
  ]
    .sort()
    .map((value) => ({ value, label: value.toUpperCase() })),
)
/** The palette's tool for a choice: the image only when it is a variant (JSON-compared). */
const tokenTool = (p: { type: string; color?: string; size?: string; image?: string }): MapTool => ({
  kind: 'token',
  type: p.type,
  color: p.color,
  size: p.size,
  ...(p.image ? { image: p.image } : {}),
})
const enemyPieces = computed(() =>
  Object.entries(props.pieces?.families ?? {})
    .map(([family, piece]) => ({
      family,
      name: pick(piece.name, language.value) || family,
      kind: piece.kind,
      href: assetUrl(figureArt(props.pieces, family).image),
    }))
    .filter((p) => matches(p.name))
    .sort((a, b) => a.name.localeCompare(b.name)),
)
const zoneTools = computed(() =>
  [START_ZONE, 'spawn', 'zone'].map((zoneKind) => ({ zoneKind, label: t(`workshop.map.zoneKind.${zoneKind}`) })),
)

const sameTool = (a: MapTool | null, b: MapTool) => JSON.stringify(a) === JSON.stringify(b)
function pickTool(next: MapTool) {
  tool.value = sameTool(tool.value, next) ? null : next
}

// Laying pieces
function place(cell: { x: number; y: number }) {
  const current = tool.value
  if (!current) return
  if (current.kind === 'tile') {
    // One of each tile: once laid, it is selected (to turn it) and the palette lets go of it
    tool.value = null
    const laid = addTile(map.value, current.ref, cell, props.pieces?.tiles[current.ref]?.pairedSide)
    if (laid === map.value) return
    changeMap(() => laid)
    selected.value = { kind: 'tile', index: laid.tiles.length - 1 }
  } else if (current.kind === 'token') {
    changeMap((m) => {
      const added = addToken(m, current, cell)
      const area = m.tiles.find(
        (tile) => cell.x >= tile.x && cell.x < tile.x + 6 && cell.y >= tile.y && cell.y < tile.y + 6,
      )?.area
      return area ? patchToken(added.map, added.id, { area }) : added.map
    })
  } else if (current.kind === 'combatant') {
    commit((b) => ({ ...b, setup: addCombatant(b.setup, current.family, cell) }))
  }
}

function draw(rect: Rect) {
  const current = tool.value
  if (current?.kind !== 'zone') return
  commit((b) => {
    const added = addZone(b.map, current.zoneKind, rect)
    selected.value = { kind: 'zone', id: added.id }
    const startHere = current.zoneKind === START_ZONE && !b.setup.adventurersStart
    return { map: added.map, setup: startHere ? { ...b.setup, adventurersStart: added.id } : b.setup }
  })
  tool.value = null
}

function move(item: MapItem, to: { x: number; y: number }) {
  commit((b) => moveItem(b, item, to))
}

function remove(item: MapItem) {
  commit((b) => removeItem(b, item))
  selected.value = null
}

function nudge(item: MapItem, dx: number, dy: number) {
  const r = rectOf(board.value, item)
  if (r) move(item, { x: Math.max(0, r.x + dx), y: Math.max(0, r.y + dy) })
}

function turn(item: MapItem) {
  if (item.kind === 'tile') changeMap((m) => rotateTile(m, item.index))
  else if (item.kind === 'token') changeMap((m) => turnToken(m, item.id))
}

function onKeydown(event: KeyboardEvent) {
  const target = event.target as HTMLElement
  if (target.closest('input, textarea, [role="combobox"], [role="listbox"]')) return
  if (event.key === 'Escape') {
    tool.value = null
    selected.value = null
    return
  }
  const item = selected.value
  if (!item) return
  const arrows: Record<string, [number, number]> = {
    ArrowLeft: [-1, 0],
    ArrowRight: [1, 0],
    ArrowUp: [0, -1],
    ArrowDown: [0, 1],
  }
  if (event.key === 'Delete' || event.key === 'Backspace') remove(item)
  else if (event.key === 'r' || event.key === 'R') turn(item)
  else if (arrows[event.key]) nudge(item, ...arrows[event.key]!)
  else return
  event.preventDefault()
}

// The inspector
const tile = computed(() => (selected.value?.kind === 'tile' ? map.value.tiles[selected.value.index] : undefined))
const token = computed(() => {
  const item = selected.value
  return item?.kind === 'token' ? map.value.tokens.find((x) => x.id === item.id) : undefined
})
const zone = computed(() => {
  const item = selected.value
  return item?.kind === 'zone' ? map.value.zones.find((x) => x.id === item.id) : undefined
})
const combatant = computed(() =>
  selected.value?.kind === 'combatant' ? setup.value.combatants[selected.value.index] : undefined,
)
const selectedRect = computed(() => (selected.value ? rectOf(board.value, selected.value) : undefined))

const tileName = (ref: string) => props.pieces?.tiles[ref]?.name ?? ref.split('/').pop() ?? ref
const tokenName = (type: string) => pick(props.pieces?.tokens[type]?.name, language.value) || type
const tokenColors = (type: string) => Object.keys(props.pieces?.tokens[type]?.colors ?? {})
/** The other pictures of a token type (its own and its colours' are shown by the colour). */
const tokenVariants = (type: string) => {
  const piece = props.pieces?.tokens[type]
  const own = new Set([piece?.image, ...Object.values(piece?.colors ?? {})])
  return tokenImages(piece).filter((image) => !own.has(image))
}
const variantName = (image: string) => variantLabel(image)
const familyOptions = computed(() => enemyPieces.value.map((p) => ({ value: p.family, label: p.name })))
const areaOptions = computed(() =>
  map.value.areas.map((a) => ({ value: a.id, label: pick(a.name, language.value) || a.id })),
)
const zoneOptions = computed(() =>
  map.value.zones.map((z) => ({ value: z.id, label: z.kind === START_ZONE ? `${z.id} · ${t('table.start')}` : z.id })),
)
const kindOptions = computed(() => zoneTools.value.map((z) => ({ value: z.zoneKind, label: z.label })))
const overlaps = computed(() => overlappingTiles(map.value).size > 0)

function renameZone(from: string, to: string) {
  const id = to.trim()
  if (!id || map.value.zones.some((z) => z.id === id)) return
  commit((b) => ({
    map: patchZone(b.map, from, { id }),
    setup: b.setup.adventurersStart === from ? { ...b.setup, adventurersStart: id } : b.setup,
  }))
  selected.value = { kind: 'zone', id }
}

function setStart(id: string | undefined) {
  commit((b) => {
    const next = { ...b.setup }
    if (id) next.adventurersStart = id
    else delete next.adventurersStart
    return { ...b, setup: next }
  })
}

function newArea() {
  changeMap((m) => addArea(m, text(language.value, t('workshop.map.areaN', { n: m.areas.length + 1 }))).map)
}
</script>

<template>
  <section class="map-editor" :aria-label="t('workshop.map.title')" tabindex="-1" @keydown="onKeydown">
    <aside class="palette" :aria-label="t('workshop.map.palette')">
      <SelectButton
        v-model="shelf"
        :options="shelves"
        option-label="label"
        option-value="value"
        :allow-empty="false"
        size="small"
        :aria-label="t('workshop.map.palette')"
      />
      <InputText
        v-if="shelf !== 'zones'"
        v-model="filter"
        :placeholder="t('workshop.map.filter')"
        :aria-label="t('workshop.map.filter')"
        size="small"
      />
      <div v-if="shelf === 'tokens'" class="filters">
        <Select
          v-model="category"
          :options="categoryOptions"
          option-label="label"
          option-value="value"
          show-clear
          size="small"
          :placeholder="t('workshop.map.allCategories')"
          :aria-label="t('workshop.map.categoryFilter')"
        />
        <Select
          v-model="pack"
          :options="packOptions"
          option-label="label"
          option-value="value"
          show-clear
          size="small"
          :placeholder="t('workshop.map.allPacks')"
          :aria-label="t('workshop.map.packFilter')"
        />
      </div>
      <p class="help">{{ tool ? t('workshop.map.placing') : t('workshop.map.pickHelp') }}</p>

      <div v-if="shelf === 'tiles'" class="shelf tiles">
        <button
          v-for="p in tilePieces"
          :key="p.ref"
          type="button"
          class="piece"
          :class="{ active: sameTool(tool, { kind: 'tile', ref: p.ref }) }"
          :aria-pressed="sameTool(tool, { kind: 'tile', ref: p.ref })"
          :aria-label="t('workshop.map.layTile', { name: p.name })"
          :disabled="p.taken"
          :title="p.taken ? t('workshop.map.tileTaken') : p.theme"
          @click="pickTool({ kind: 'tile', ref: p.ref })"
        >
          <img v-if="p.href" :src="p.href" alt="" loading="lazy" />
          <span>{{ p.name }}</span>
          <small>{{ p.theme }}</small>
        </button>
      </div>

      <div v-else-if="shelf === 'tokens'" class="shelf tokens">
        <button
          v-for="p in tokenPieces"
          :key="`${p.type}|${p.color ?? ''}|${p.image ?? ''}`"
          type="button"
          class="piece row"
          :class="{ active: sameTool(tool, tokenTool(p)) }"
          :aria-pressed="sameTool(tool, tokenTool(p))"
          :aria-label="t('workshop.map.layToken', { name: p.variant ? `${p.name} (${p.variant})` : p.name })"
          :title="p.pack.toUpperCase()"
          @click="pickTool(tokenTool(p))"
        >
          <img v-if="p.href" :src="assetUrl(p.href)" alt="" loading="lazy" />
          <span>
            {{ p.name }}
            <small v-if="p.variant">{{ p.variant }}</small>
          </span>
        </button>
      </div>

      <div v-else-if="shelf === 'enemies'" class="shelf tokens">
        <button
          v-for="p in enemyPieces"
          :key="p.family"
          type="button"
          class="piece row"
          :class="{ active: sameTool(tool, { kind: 'combatant', family: p.family }) }"
          :aria-pressed="sameTool(tool, { kind: 'combatant', family: p.family })"
          :aria-label="t('workshop.map.layEnemy', { name: p.name })"
          @click="pickTool({ kind: 'combatant', family: p.family })"
        >
          <img v-if="p.href" :src="p.href" alt="" loading="lazy" class="mini" />
          <i v-else class="pi pi-user mini" aria-hidden="true" />
          <span>{{ p.name }}</span>
        </button>
      </div>

      <div v-else class="shelf zones">
        <button
          v-for="z in zoneTools"
          :key="z.zoneKind"
          type="button"
          class="piece row"
          :class="{ active: sameTool(tool, { kind: 'zone', zoneKind: z.zoneKind }) }"
          :aria-pressed="sameTool(tool, { kind: 'zone', zoneKind: z.zoneKind })"
          @click="pickTool({ kind: 'zone', zoneKind: z.zoneKind })"
        >
          <i class="pi pi-stop" aria-hidden="true" />
          <span>{{ z.label }}</span>
        </button>
        <p class="help">{{ t('workshop.map.zoneHelp') }}</p>

        <h3>{{ t('workshop.map.fog') }}</h3>
        <p class="help">{{ t('workshop.map.fogHelp') }}</p>
        <ul class="areas">
          <li v-for="area in map.areas" :key="area.id">
            <InputText
              :model-value="pick(area.name, language)"
              :aria-label="t('workshop.map.areaName', { id: area.id })"
              size="small"
              @update:model-value="
                (v) => changeMap((m) => patchArea(m, area.id, { name: setText(area.name, language, v ?? '') }))
              "
            />
            <label class="inline">
              <ToggleSwitch
                :model-value="!!area.revealedAtStart"
                :aria-label="t('workshop.map.revealedAtStart', { id: area.id })"
                @update:model-value="(v) => changeMap((m) => patchArea(m, area.id, { revealedAtStart: v }))"
              />
              {{ t('workshop.map.visible') }}
            </label>
            <Button
              icon="pi pi-times"
              size="small"
              text
              severity="secondary"
              :aria-label="t('workshop.map.removeArea', { id: area.id })"
              @click="changeMap((m) => removeArea(m, area.id))"
            />
          </li>
        </ul>
        <Button :label="t('workshop.map.addArea')" icon="pi pi-plus" size="small" text @click="newArea" />
      </div>
    </aside>

    <MapCanvas
      class="board"
      :map="map"
      :setup="setup"
      :pieces="pieces"
      :selected="selected"
      :tool="tool"
      @select="(item) => (selected = item)"
      @move="move"
      @place="place"
      @draw="draw"
    >
      <template #tools>
        <Button
          :label="t('workshop.map.back')"
          icon="pi pi-arrow-left"
          size="small"
          severity="secondary"
          @click="emit('close')"
        />
        <Button
          v-if="tool"
          :label="t('workshop.map.stopPlacing')"
          icon="pi pi-times"
          size="small"
          text
          @click="tool = null"
        />
      </template>
    </MapCanvas>

    <aside class="inspector" :aria-label="t('workshop.map.inspector')">
      <template v-if="selected && selectedRect">
        <header>
          <h3 v-if="tile">{{ t('workshop.map.tile', { name: tileName(tile.ref) }) }}</h3>
          <h3 v-else-if="token">{{ tokenName(token.type) }}</h3>
          <h3 v-else-if="zone">{{ t('workshop.map.zone') }}</h3>
          <h3 v-else-if="combatant">{{ t('workshop.map.enemy') }}</h3>
          <Button
            icon="pi pi-trash"
            size="small"
            severity="danger"
            text
            :aria-label="t('workshop.map.removePiece')"
            :title="t('workshop.map.removePiece')"
            @click="remove(selected)"
          />
        </header>
        <div class="xy">
          <label>
            x
            <InputNumber
              :model-value="selectedRect.x"
              :min="0"
              show-buttons
              size="small"
              input-class="num"
              :aria-label="t('workshop.map.column')"
              @update:model-value="(v) => move(selected!, { x: v ?? 0, y: selectedRect!.y })"
            />
          </label>
          <label>
            y
            <InputNumber
              :model-value="selectedRect.y"
              :min="0"
              show-buttons
              size="small"
              input-class="num"
              :aria-label="t('workshop.map.row')"
              @update:model-value="(v) => move(selected!, { x: selectedRect!.x, y: v ?? 0 })"
            />
          </label>
        </div>

        <template v-if="tile && selected.kind === 'tile'">
          <div class="line">
            <span>{{ t('workshop.map.rotation', { degrees: tile.rotation ?? 0 }) }}</span>
            <Button
              icon="pi pi-replay"
              size="small"
              text
              :aria-label="t('workshop.map.rotateLeft')"
              @click="changeMap((m) => rotateTile(m, (selected as { index: number }).index, -90))"
            />
            <Button
              icon="pi pi-refresh"
              size="small"
              text
              :aria-label="t('workshop.map.rotateRight')"
              @click="turn(selected)"
            />
          </div>
          <label class="field">
            {{ t('workshop.map.fogArea') }}
            <Select
              :model-value="tile.area"
              :options="areaOptions"
              option-label="label"
              option-value="value"
              show-clear
              :placeholder="t('workshop.map.noFog')"
              :aria-label="t('workshop.map.fogArea')"
              @update:model-value="
                (v) => changeMap((m) => setTileArea(m, (selected as { index: number }).index, v ?? undefined))
              "
            />
          </label>
          <p v-if="!areaOptions.length" class="help">{{ t('workshop.map.noAreaYet') }}</p>
        </template>

        <template v-else-if="token">
          <label v-if="tokenColors(token.type).length" class="field">
            {{ t('workshop.map.tokenColor') }}
            <Select
              :model-value="token.color"
              :options="
                tokenColors(token.type).map((value) => ({ value, label: t(`workshop.map.color.${value}`, value) }))
              "
              option-label="label"
              option-value="value"
              :aria-label="t('workshop.map.tokenColor')"
              @update:model-value="(v) => changeMap((m) => patchToken(m, token!.id, { color: v }))"
            />
          </label>
          <fieldset v-if="tokenVariants(token.type).length" class="field images">
            <legend>{{ t('workshop.map.tokenImage') }}</legend>
            <button
              v-for="choice in [undefined, ...tokenVariants(token.type)]"
              :key="choice ?? ''"
              type="button"
              class="image-choice"
              :class="{ active: (token.image ?? undefined) === choice }"
              :aria-pressed="(token.image ?? undefined) === choice"
              :aria-label="
                choice ? t('workshop.map.chooseImage', { name: variantName(choice) }) : t('workshop.map.defaultImage')
              "
              :title="choice ? variantName(choice) : t('workshop.map.defaultImage')"
              @click="changeMap((m) => patchToken(m, token!.id, { image: choice }))"
            >
              <img :src="assetUrl(choice ?? tokenImage({ color: token.color }, pieces?.tokens[token.type]))" alt="" />
            </button>
          </fieldset>
          <Button
            v-if="(token.w ?? 1) !== (token.h ?? 1)"
            :label="t('workshop.map.turnToken')"
            icon="pi pi-sync"
            size="small"
            text
            @click="turn(selected)"
          />
          <p class="help">
            {{ t('workshop.map.tokenKey', { key: tokenKey(token.type, token.color) }) }}
            <template v-if="token.area">· {{ t('workshop.map.inArea', { area: token.area }) }}</template>
          </p>
        </template>

        <template v-else-if="zone">
          <label class="field">
            {{ t('workshop.map.zoneId') }}
            <InputText
              :model-value="zone.id"
              size="small"
              :aria-label="t('workshop.map.zoneId')"
              @change="(e: Event) => renameZone(zone!.id, (e.target as HTMLInputElement).value)"
            />
          </label>
          <label class="field">
            {{ t('workshop.map.zoneKindLabel') }}
            <Select
              :model-value="zone.kind ?? 'zone'"
              :options="kindOptions"
              option-label="label"
              option-value="value"
              :aria-label="t('workshop.map.zoneKindLabel')"
              @update:model-value="(v) => changeMap((m) => patchZone(m, zone!.id, { kind: v }))"
            />
          </label>
          <div class="xy">
            <label>
              {{ t('workshop.map.width') }}
              <InputNumber
                :model-value="zone.rect.w"
                :min="1"
                show-buttons
                size="small"
                input-class="num"
                @update:model-value="
                  (v) => changeMap((m) => patchZone(m, zone!.id, { rect: { ...zone!.rect, w: v ?? 1 } }))
                "
              />
            </label>
            <label>
              {{ t('workshop.map.height') }}
              <InputNumber
                :model-value="zone.rect.h"
                :min="1"
                show-buttons
                size="small"
                input-class="num"
                @update:model-value="
                  (v) => changeMap((m) => patchZone(m, zone!.id, { rect: { ...zone!.rect, h: v ?? 1 } }))
                "
              />
            </label>
          </div>
        </template>

        <template v-else-if="combatant">
          <label class="field">
            {{ t('workshop.map.family') }}
            <Select
              :model-value="combatant.family"
              :options="familyOptions"
              option-label="label"
              option-value="value"
              filter
              :aria-label="t('workshop.map.family')"
              @update:model-value="
                (v) =>
                  commit((b) => ({
                    ...b,
                    setup: {
                      ...b.setup,
                      combatants: b.setup.combatants.map((c, i) =>
                        i === (selected as { index: number }).index ? { ...c, family: v } : c,
                      ),
                    },
                  }))
              "
            />
          </label>
          <p class="help">{{ t('workshop.map.familyHelp') }}</p>
        </template>
        <p class="help">{{ t('workshop.map.keys') }}</p>
      </template>

      <template v-else>
        <h3>{{ t('workshop.map.summary') }}</h3>
        <ul class="counts">
          <li>{{ t('workshop.map.countTiles', map.tiles.length) }}</li>
          <li>{{ t('workshop.map.countTokens', map.tokens.length) }}</li>
          <li>{{ t('workshop.map.countEnemies', setup.combatants.length) }}</li>
        </ul>
        <label class="field">
          {{ t('workshop.map.adventurersStart') }}
          <Select
            :model-value="setup.adventurersStart"
            :options="zoneOptions"
            option-label="label"
            option-value="value"
            show-clear
            :placeholder="t('workshop.map.noStart')"
            :aria-label="t('workshop.map.adventurersStart')"
            @update:model-value="(v) => setStart(v ?? undefined)"
          />
        </label>
        <p v-if="overlaps" class="warn"><i class="pi pi-exclamation-triangle" /> {{ t('workshop.map.overlap') }}</p>
        <p class="help">{{ t('workshop.map.howTo') }}</p>
      </template>
    </aside>
  </section>
</template>

<style scoped>
.map-editor {
  display: grid;
  grid-template-columns: 17rem minmax(0, 1fr) 17rem;
  gap: 0.75rem;
  min-height: 0;
  height: 100%;
  outline: none;
}
.palette,
.inspector {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-height: 0;
  overflow-y: auto;
  padding: 0.6rem;
  border: 1px solid var(--p-content-border-color);
  border-radius: var(--p-content-border-radius);
}
.board {
  min-height: 0;
}
.shelf {
  display: grid;
  gap: 0.35rem;
}
.shelf.tiles {
  grid-template-columns: repeat(3, 1fr);
}
.piece {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.2rem;
  padding: 0.25rem;
  border: 2px solid transparent;
  border-radius: 6px;
  background: var(--p-content-hover-background);
  color: inherit;
  font: inherit;
  font-size: 0.75rem;
  cursor: pointer;
}
.piece.row {
  flex-direction: row;
  justify-content: flex-start;
  text-align: left;
}
.piece img {
  width: 100%;
  aspect-ratio: 1;
  object-fit: cover;
  border-radius: 4px;
}
.piece.row img,
.piece .mini {
  width: 2rem;
  height: 2rem;
  flex: none;
  object-fit: contain;
}
.piece .mini {
  border-radius: 50%;
  object-fit: cover;
  object-position: left;
  display: grid;
  place-items: center;
}
.piece.row span {
  display: flex;
  flex-direction: column;
}
.filters {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 0.35rem;
}
.images {
  display: flex;
  flex-flow: row wrap;
  gap: 0.3rem;
  margin: 0;
  padding: 0.3rem 0 0;
  border: none;
}
.images legend {
  padding: 0;
  margin-bottom: 0.2rem;
}
.image-choice {
  width: 2.6rem;
  height: 2.6rem;
  padding: 0;
  border: 2px solid transparent;
  border-radius: 4px;
  background: none;
  cursor: pointer;
}
.image-choice img {
  width: 100%;
  height: 100%;
  object-fit: contain;
}
.image-choice.active {
  border-color: #fbbf24;
}
.piece small {
  font-size: 0.65rem;
  color: var(--p-text-muted-color);
}
.piece:disabled {
  opacity: 0.35;
  cursor: not-allowed;
}
.piece.active {
  border-color: #fbbf24;
  background: rgb(251 191 36 / 0.15);
}
.piece:focus-visible {
  outline: 2px solid var(--p-primary-color);
}
.help {
  margin: 0;
  font-size: 0.8rem;
  color: var(--p-text-muted-color);
}
.warn {
  margin: 0;
  font-size: 0.85rem;
  color: #dc2626;
}
h3 {
  margin: 0.4rem 0 0;
  font-size: 0.95rem;
}
header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.xy {
  display: flex;
  gap: 0.5rem;
}
.xy label,
.field {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--p-text-muted-color);
}
.xy :deep(.num) {
  width: 3.5rem;
}
.line {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}
.line span {
  flex: 1;
}
.areas,
.counts {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.areas li {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 0.3rem;
  align-items: center;
}
.inline {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  font-size: 0.75rem;
}
@media (max-width: 1100px) {
  .map-editor {
    grid-template-columns: 1fr;
    height: auto;
  }
  .board {
    height: 60vh;
  }
}
</style>
