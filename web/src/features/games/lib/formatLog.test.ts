import { describe, expect, it } from 'vitest'
import { formatLog } from './formatLog'
import { shortId } from './ids'

const messages: Record<string, string> = {
  'log.turn': 'Tour : {who}',
  'log.gainItem': 'Objet gagné : {item}',
  'log.hiddenText': 'Texte caché',
  'log.unknown': '{code}',
  'log.answer': 'Réponse : {answer}',
  'prompt.yes': 'Oui',
}
const translate = (key: string, args: Record<string, string>) =>
  (messages[key] ?? key).replace(/\{(\w+)\}/g, (_, name: string) => args[name] ?? '')
const has = (key: string) => key in messages

describe('formatLog', () => {
  it('names the adventurer whose turn it is', () => {
    const line = formatLog(
      { code: 'turn', args: { card: 'adventurer:Rook', adventurer: 'Rook' } },
      'fr',
      translate,
      has,
    )

    expect(line.title).toBe('Tour : Rook')
    expect(line.tone).toBe('round')
  })

  it('shortens the initiative card of a combatant turn', () => {
    const line = formatLog({ code: 'turn', args: { card: 'um1:initiative/cave-sickle-1-3' } }, 'fr', translate, has)

    expect(line.title).toBe('Tour : cave-sickle-1-3')
  })

  it('describes an item gained by tier and type', () => {
    const line = formatLog(
      { code: 'gainItem', args: { count: '2', tiers: 'mundane/common', type: 'consumable' } },
      'fr',
      translate,
      has,
    )

    expect(line.title).toBe('Objet gagné : 2 mundane/common consumable')
  })

  it('keeps the scenario text in the reader language, falling back to English', () => {
    const line = formatLog({ code: 'hiddenText', text: { en: 'A chest floats downstream.' } }, 'fr', translate, has)

    expect(line.text).toBe('A chest floats downstream.')
    expect(line.tone).toBe('narrative')
  })

  it('translates a fixed answer', () => {
    const line = formatLog({ code: 'answer', args: { question: 'aiCondition', answer: 'yes' } }, 'fr', translate, has)

    expect(line.title).toBe('Réponse : Oui')
  })

  it('shows the raw code of a line it does not know', () => {
    const line = formatLog({ code: 'somethingNew' }, 'en', translate, has)

    expect(line.title).toBe('somethingNew')
    expect(line.tone).toBe('info')
  })
})

describe('shortId', () => {
  it.each([
    ['um1:combatant/fragor-1-3-common', 'fragor-1-3-common'],
    ['adventurer:Nightingale', 'Nightingale'],
    ['loot:red', 'loot:red'],
  ])('%s → %s', (id, expected) => {
    expect(shortId(id)).toBe(expected)
  })
})
