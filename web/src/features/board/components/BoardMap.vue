<script setup lang="ts">
import { computed, useId } from 'vue'
import { useI18n } from 'vue-i18n'
import { storeToRefs } from 'pinia'
import Message from 'primevue/message'
import Skeleton from 'primevue/skeleton'
import Tag from 'primevue/tag'
import type { Combatant } from '@/features/catalog'
import type { EncounterState } from '@/features/games'
import type { ScenarioNode } from '@/features/scenarios'
import { pick } from '@/shared/lib/localized'
import { usePreferences } from '@/shared/lib/preferences'
import { useBoardPieces } from '../api/queries'
import { boardLayout, type FigureShape, type TokenShape } from '../lib/layout'

const props = defineProps<{
  node: ScenarioNode | undefined
  encounter: EncounterState
  cards: Map<string, Combatant>
  /** Board reports are not allowed right now (a question is pending). */
  locked: boolean
}>()
const emit = defineEmits<{ activateToken: [key: string] }>()

/** Pixels per square in the SVG coordinates (the drawing scales to the available width). */
const S = 100

const { t } = useI18n()
const { language } = storeToRefs(usePreferences())
const pieces = useBoardPieces()
const layout = computed(() => boardLayout(props.node, props.encounter, pieces.data.value))
const clipId = `figure-clip-${useId()}`

function figureTitle(figure: FigureShape): string {
  const card = props.cards.get(figure.card)
  return card ? `${figure.instance} · ${pick(card.name, language.value)}` : figure.instance
}

function activatable(token: TokenShape): boolean {
  return !!props.node?.tokens[token.key]
}

function tokenTitle(token: TokenShape): string {
  const rule = props.node?.tokens[token.key]
  return rule ? `${token.key} — ${pick(rule.activation, language.value)}` : token.key
}

function activate(token: TokenShape) {
  if (activatable(token) && !props.locked) emit('activateToken', token.key)
}
</script>

<template>
  <figure class="board">
    <Skeleton v-if="pieces.isPending.value" height="16rem" />
    <Message v-else-if="pieces.isError.value" severity="warn">{{ t('board.unavailable') }}</Message>
    <svg
      v-else
      :viewBox="`0 0 ${layout.width * S} ${layout.height * S}`"
      role="img"
      :aria-label="t('board.title', { name: pick(node?.name, language) })"
    >
      <defs>
        <clipPath :id="clipId" clipPathUnits="objectBoundingBox">
          <circle cx="0.5" cy="0.5" r="0.5" />
        </clipPath>
      </defs>

      <g v-for="tile in layout.tiles" :key="tile.key" class="tile">
        <title>{{ t('board.tile', { name: tile.label }) }}</title>
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
        <rect class="tile-edge" :x="tile.x * S" :y="tile.y * S" :width="tile.w * S" :height="tile.h * S" />
      </g>

      <g v-for="area in layout.elevation" :key="area.key" class="elevation">
        <rect :x="area.x * S" :y="area.y * S" :width="area.w * S" :height="area.h * S" />
        <text :x="(area.x + 0.1) * S" :y="(area.y + 0.3) * S">{{ t('board.elevation', { level: area.level }) }}</text>
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
          {{ zone.kind === 'startingZone' ? t('board.start') : zone.key }}
        </text>
      </g>

      <g
        v-for="current in layout.currents"
        :key="current.key"
        class="current"
        :transform="`translate(${current.x * S} ${current.y * S}) rotate(${current.angle})`"
      >
        <title>{{ t('board.current', { strength: current.strength }) }}</title>
        <path d="M -120 0 L 80 0 M 40 -40 L 100 0 L 40 40" />
      </g>

      <g
        v-for="token in layout.tokens"
        :key="token.id"
        class="token"
        :class="{ activatable: activatable(token), locked }"
        :role="activatable(token) ? 'button' : undefined"
        :aria-label="activatable(token) ? t('board.activate', { token: token.key }) : undefined"
        :aria-disabled="activatable(token) ? locked : undefined"
        :tabindex="activatable(token) && !locked ? 0 : undefined"
        @click="activate(token)"
        @keydown.enter="activate(token)"
      >
        <title>{{ tokenTitle(token) }}</title>
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
      </g>

      <g
        v-for="figure in layout.figures"
        :key="figure.instance"
        class="figure"
        :class="{ acting: figure.acting, aggro: figure.aggro }"
      >
        <title>{{ figureTitle(figure) }}</title>
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
          preserveAspectRatio="xMidYMid slice"
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

    <figcaption>
      <span class="note">{{ t('board.setupNote') }}</span>
      <span v-if="layout.unplacedFigures.length" class="unplaced">
        {{ t('board.unplacedFigures') }}
        <Tag v-for="c in layout.unplacedFigures" :key="c.instance" :value="c.instance" severity="warn" />
      </span>
      <span v-if="layout.unplacedTokens.length" class="unplaced">
        {{ t('board.unplacedTokens') }}
        <Tag v-for="tk in layout.unplacedTokens" :key="tk.id" :value="tk.key" severity="info" />
      </span>
    </figcaption>
  </figure>
</template>

<style scoped>
.board {
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
svg {
  width: 100%;
  height: auto;
  max-height: 60vh;
  background: #1b1b1b;
  border-radius: var(--p-content-border-radius);
}
.tile-edge {
  fill: none;
  stroke: rgb(0 0 0 / 0.6);
  stroke-width: 3;
}
.missing {
  fill: #333;
  stroke: #666;
  stroke-dasharray: 10 6;
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
.token.activatable {
  cursor: pointer;
}
.token.activatable.locked {
  cursor: not-allowed;
}
.token.activatable:hover image,
.token.activatable:focus-visible image {
  filter: brightness(1.25) drop-shadow(0 0 8px #fbbf24);
}
.token:focus-visible {
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
.figure.aggro .ring {
  stroke-dasharray: 14 8;
}
.figure.acting .ring {
  stroke: #f59e0b;
  stroke-width: 12;
  animation: pulse 1.2s ease-in-out infinite;
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
figcaption {
  display: flex;
  flex-wrap: wrap;
  gap: 0.4rem 1rem;
  align-items: center;
  font-size: 0.85rem;
  color: var(--p-text-muted-color);
}
.unplaced {
  display: inline-flex;
  flex-wrap: wrap;
  gap: 0.3rem;
  align-items: center;
}
</style>
