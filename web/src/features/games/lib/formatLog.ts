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
}

const tones: Record<string, LogTone> = {
  scene: 'narrative',
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
  roundStart: 'round',
  roundEnd: 'round',
  turn: 'round',
  urgency: 'alert',
  urgencyDefeat: 'alert',
  manualAction: 'alert',
  spawnImpossible: 'alert',
  noTokenLeft: 'alert',
  encounterEnd: 'reward',
  reward: 'reward',
  gainGold: 'reward',
  gainXp: 'reward',
  gainItem: 'reward',
  revealHiddenCard: 'reward',
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
  scenarioEnd: 'reward',
  answer: 'muted',
  figureTurnEnd: 'muted',
}

/** Arguments made readable: content ids shortened, the turn's owner named. */
function readableArgs(entry: LogEntry): Record<string, string> {
  const args: Record<string, string> = { ...entry.args }
  for (const key of ['card', 'from', 'to', 'item', 'diagram']) {
    const value = args[key]
    if (value) args[key] = shortId(value)
  }
  if (entry.code === 'turn') args.who = args.adventurer ?? shortId(args.card ?? '')
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
  const key = `log.${entry.code}`
  let title = hasMessage(key) ? translate(key, args) : translate('log.unknown', { code: entry.code })
  // STATUS Effects resolve at the start of the figure's turn (Poison, Condemn, Haste)
  if (args.statusEffects) title += ` · ${translate('log.statusEffects', { effects: args.statusEffects })}`
  return { title, text: pick(entry.text, language), tone: tones[entry.code] ?? 'info' }
}
