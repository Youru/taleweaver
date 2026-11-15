import { describe, expect, it } from 'vitest'
import type { DraftScenario } from '../api/types'
import { COLUMN, exitsOf, freshId, layout, newEncounter, newScene, removeNode, setChoice, setExit, slug } from './graph'

const labels = { question: { fr: 'Que faites-vous ?' }, option: (n: number) => ({ fr: `Option ${n}` }) }

function scenario(): DraftScenario {
  const intro = { ...newScene('intro', 'fr', 'Le départ'), next: 'fight' }
  const fight = newEncounter('fight', 'fr', { name: 'Embuscade', win: 'Tous vaincus', lose: 'Tous à terre' })
  return { id: 'homebrew:scenario/test', title: { fr: 'Test' }, packs: ['um1'], start: 'intro', nodes: [intro, fight] }
}

describe('exits', () => {
  it('gives a scene its next, an encounter its end conditions', () => {
    const s = scenario()
    expect(exitsOf(s.nodes[0]!)).toEqual([{ key: 'next', kind: 'next', target: 'fight' }])
    expect(exitsOf(s.nodes[1]!).map((e) => [e.key, e.kind, e.target])).toEqual([
      ['end:win', 'win', '@victory'],
      ['end:lose', 'lose', '@defeat'],
    ])
  })

  it('turns a next into a choice and back, keeping where the first option leads', () => {
    const intro = scenario().nodes[0]!
    const choice = setChoice(intro, true, labels)
    expect(choice.next).toBeUndefined()
    expect(exitsOf(choice).map((e) => [e.key, e.target])).toEqual([
      ['option:0', 'fight'],
      ['option:1', undefined],
    ])
    const branched = setExit(choice, 'option:1', '@defeat')
    expect(branched.mechanics?.[0]?.options?.[1]?.actions).toEqual([{ do: 'goToNode', node: '@defeat' }])
    expect(setChoice(branched, false, labels)).toMatchObject({ next: 'fight', mechanics: [] })
  })

  it('links an end condition to another node', () => {
    const fight = setExit(scenario().nodes[1]!, 'end:lose', 'intro')
    expect(fight.endConditions?.[1]?.next).toBe('intro')
    expect(fight.endConditions?.[0]?.next).toBe('@victory')
  })
})

describe('layout', () => {
  it('lays the nodes out step by step from the start, the endings last', () => {
    const positions = layout(scenario())
    expect(positions.intro).toEqual({ x: 0, y: 0 })
    expect(positions.fight).toEqual({ x: COLUMN, y: 0 })
    expect(positions['@victory']).toEqual({ x: 2 * COLUMN, y: 0 })
    expect(positions['@defeat']?.x).toBe(2 * COLUMN)
  })

  it('keeps the positions the author chose', () => {
    const s = { ...scenario(), editor: { positions: { fight: { x: 40, y: 500 } } } }
    expect(layout(s).fight).toEqual({ x: 40, y: 500 })
  })
})

describe('editing', () => {
  it('removes a node and the links that led to it', () => {
    const s = removeNode(scenario(), 'fight')
    expect(s.nodes.map((n) => n.id)).toEqual(['intro'])
    expect(s.nodes[0]?.next).toBeUndefined()
  })

  it('names new nodes and files', () => {
    expect(freshId(scenario(), 'scene')).toBe('scene-1')
    expect(slug('La Prime du Marais !')).toBe('la-prime-du-marais')
  })
})
