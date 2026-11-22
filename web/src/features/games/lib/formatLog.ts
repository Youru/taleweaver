import { pick, type Language } from '@/shared/lib/localized'
import type { LogEntry } from '../api/types'
import { shortId } from './ids'

export type LogTone = 'narrative' | 'ai' | 'round' | 'alert' | 'reward' | 'info' | 'muted'

export interface FormattedLog {
  /** Line written by the app, in the reader's language (may be empty for plain texts). */
  title: string
  /** Text of the scenario or of the card, as written. */
  text: string
  tone: LogTone
  /** A scene or the start of an encounter: shown as a page of the book (banner, parchment). */
  story?: StoryHeading
}

export interface StoryHeading {
  /** "Scene", "Encounter". */
  kicker: string
  /** The scene's title or the encounter's name. */
  heading: string
  /** Under the heading: the encounter's loot level. */
  detail?: string
}

const tones: Record<string, LogTone> = {
  scene: 'narrative',
  storyRound: 'narrative',
  bountyStart: 'narrative',
  bountyEnd: 'reward',
  consumableDiscarded: 'muted',
  upgradeUnlocked: 'reward',
  upgradeAttached: 'reward',
  upgradeDetached: 'muted',
  disciplineLearnt: 'reward',
  disciplineForgotten: 'muted',
  respec: 'muted',
  storyRoundReturn: 'narrative',
  sideQuest: 'narrative',
  ventureForth: 'narrative',
  storeUnique: 'reward',
  storeUniqueNone: 'muted',
  storyRoundExplore: 'info',
  joinParty: 'reward',
  leaveParty: 'alert',
  chooseActiveGroup: 'alert',
  activeGroup: 'info',
  adventurerStatus: 'info',
  encounter: 'narrative',
  hiddenText: 'narrative',
  hiddenRule: 'narrative',
  text: 'narrative',
  specialRule: 'narrative',
  placeDiagram: 'narrative',
  navigate: 'narrative',
  initiativeRebuilt: 'round',
  addEndCondition: 'narrative',
  figureTurn: 'ai',
  aiInstruction: 'ai',
  aggro: 'ai',
  targeted: 'muted',
  paralyzed: 'ai',
  paralyzeEnded: 'muted',
  effectImmune: 'alert',
  roundStart: 'round',
  roundEnd: 'round',
  turn: 'round',
  urgency: 'alert',
  urgencyDefeat: 'alert',
  adventurerDamage: 'alert',
  adventurerDefeated: 'alert',
  partyDefeated: 'alert',
  stamina: 'muted',
  adventurersPrepared: 'muted',
  manualAction: 'alert',
  spawnImpossible: 'alert',
  noTokenLeft: 'alert',
  encounterEnd: 'reward',
  encounterContinues: 'reward',
  reward: 'reward',
  gainGold: 'reward',
  gainXp: 'reward',
  gainItem: 'reward',
  revealHiddenCard: 'reward',
  gainDiscipline: 'reward',
  achievement: 'reward',
  achievementText: 'narrative',
  abraxisStart: 'narrative',
  abraxisRound: 'info',
  abraxisEnd: 'reward',
  adventurerReturns: 'alert',
  lootGold: 'reward',
  possibleSpawn: 'alert',
  lootItem: 'reward',
  lootMonster: 'reward',
  lootUnique: 'reward',
  lootDeckEmpty: 'muted',
  lootMonsterDiscarded: 'muted',
  lootUniqueNone: 'muted',
  lootNone: 'muted',
  lootDeckShuffled: 'muted',
  itemAdded: 'reward',
  itemSold: 'reward',
  itemUsed: 'info',
  itemMoved: 'muted',
  itemDiscarded: 'muted',
  itemFlipped: 'muted',
  scenarioEnd: 'reward',
  setOut: 'narrative',
  allySummoned: 'reward',
  allyJoined: 'reward',
  allyStays: 'info',
  allyPlaced: 'info',
  allyDamage: 'alert',
  allyDefeated: 'alert',
  allyDismissed: 'muted',
  summonToken: 'muted',
  conduitEarned: 'reward',
  conduitForm: 'info',
  conduitLeft: 'muted',
  answer: 'muted',
  participantMissing: 'alert',
  noExitOpen: 'alert',
  rewardTaken: 'alert',
  carriedDropped: 'alert',
  carriedLost: 'alert',
  healBlocked: 'alert',
  effectBlocked: 'alert',
  secondMove: 'alert',
  staminaShort: 'alert',
  reaction: 'alert',
  poppetBreak: 'alert',
  tentacleTaken: 'alert',
  tentacleRemoved: 'alert',
  tokenLimit: 'alert',
  stashOutsideStoryRound: 'alert',
  delayedDue: 'alert',
  currentReminder: 'alert',
  currentTokensReminder: 'alert',
  terrainReminder: 'alert',
  summonerMissing: 'alert',
  skillFailed: 'alert',
  skillPassed: 'reward',
  noInstruction: 'ai',
  corrected: 'alert',
  asleep: 'ai',
  spent: 'muted',
  cardMarked: 'muted',
  abilityToken: 'muted',
  reequipped: 'muted',
  tetherRemoved: 'muted',
  skillCheck: 'muted',
  figureTurnEnd: 'muted',
}

/** How a line of the journal reads at a glance (its colour), by code. */
export const toneOf = (code: string): LogTone => tones[code] ?? 'info'

/** Arguments made readable: content ids shortened, the turn's owner named. */
function readableArgs(entry: LogEntry): Record<string, string> {
  const args: Record<string, string> = { ...entry.args }
  for (const key of ['card', 'from', 'to', 'item', 'diagram', 'token', 'upgrade', 'discipline', 'scenario']) {
    const value = args[key]
    if (value) args[key] = shortId(value)
  }
  for (const key of ['instance', 'figure', 'target']) {
    const value = args[key]
    if (value?.startsWith('adventurer:') || value?.startsWith('ally:')) args[key] = value.slice(value.indexOf(':') + 1)
  }
  if (entry.code === 'turn') args.who = args.adventurer ?? args.ally ?? shortId(args.card ?? '')
  if (entry.code === 'gainItem' && !args.item) args.item = [args.count, args.tiers, args.type].filter(Boolean).join(' ')
  return args
}

/** How a line of the engine's journal reads. <paramref name="translate"/> is vue-i18n's t. */
export function formatLog(
  entry: LogEntry,
  language: Language,
  translate: (key: string, args: Record<string, string>) => string,
  hasMessage: (key: string) => boolean,
): FormattedLog {
  const args = readableArgs(entry)
  // Answers given with a fixed option (yes, no, continue) read in the reader's language.
  const fixed = entry.code === 'answer' && args.answer ? `prompt.${args.answer}` : null
  if (fixed && hasMessage(fixed)) args.answer = translate(fixed, {})
  // The party's items: the stash and the items to hand out in words, a purchase and a token placed told apart
  if (entry.code.startsWith('item')) {
    const owner = args.owner
    args.owner =
      owner === 'stash' || owner === 'unassigned'
        ? translate(`log.itemOwner.${owner}`, {})
        : (owner ?? translate('log.itemOwner.nobody', {}))
    if (args.slot === 'pack') args.owner += ` (${translate('inventory.pack', {})})`
    if (!args.item) args.item = args.id ?? ''
  }
  // The codes of the turn, in the reader's words
  for (const [name, group] of [
    ['action', 'turnTools.action'],
    ['kind', 'turnTools.kind'],
    ['mark', 'marks.mark'],
    ['token', 'marks.token'],
  ] as const) {
    const key = `${group}.${args[name]}`
    if (args[name] && hasMessage(key)) args[name] = translate(key, {})
  }
  if (entry.code === 'corrected') {
    const field = `log.correctedField.${args.field}`
    if (hasMessage(field)) args.field = translate(field, {})
    const about = args.about ? ` (${shortId(args.about)})` : ''
    args.change = `${about}${args.from !== undefined ? ` ${args.from} →` : ''} ${args.to ?? '–'}`
  }
  const code =
    entry.code === 'itemAdded' && args.price
      ? 'itemBought'
      : entry.code === 'itemUsed' && args.token
        ? 'itemUsedToken'
        : (entry.code === 'gainXp' || entry.code === 'gainItem') && args.adventurer
          ? `${entry.code}To`
          : entry.code
  const key = `log.${code}`
  let title = hasMessage(key) ? translate(key, args) : translate('log.unknown', { code: entry.code })
  // STATUS Effects resolve at the start of the figure's turn (Poison, Condemn, Haste)
  if (args.statusEffects) title += ` · ${translate('log.statusEffects', { effects: args.statusEffects })}`
  // Who decided, or tested (play-spec A5, A20)
  if ((entry.code === 'answer' || entry.code === 'skillCheck') && args.adventurer) title += ` · ${args.adventurer}`
  // Who plays a guest of the party
  if (args.playedBy) title += ` · ${translate('log.playedBy', { name: args.playedBy })}`
  // A player's request accepted by the GM (room-spec §9): who asked for it
  if (args.by) title += ` · ${translate('log.by', { name: args.by })}`
  const line: FormattedLog = { title, text: pick(entry.text, language), tone: toneOf(entry.code) }
  const heading =
    entry.code === 'scene'
      ? args.title
      : entry.code === 'encounter' || entry.code === 'storyRound' || entry.code === 'storyRoundReturn'
        ? args.name ?? args.node
        : undefined
  const lootLevelName = (level: string) =>
    hasMessage(`workshop.loot.${level}`) ? translate(`workshop.loot.${level}`, {}) : level
  // The end of an encounter opens the page that follows it: how it ended, then the reward
  if (entry.code === 'encounterEnd') {
    line.story = {
      kicker: translate('log.story.encounterEnd', {}),
      heading: translate(args.kind === 'lose' ? 'log.story.lose' : 'log.story.win', {}),
    }
    return line
  }
  if (heading)
    line.story = {
      kicker: translate(`log.story.${entry.code}`, {}),
      heading,
      ...(entry.code === 'encounter' && args.lootLevel
        ? { detail: translate('log.story.loot', { lootLevel: lootLevelName(args.lootLevel) }) }
        : {}),
    }
  return line
}
