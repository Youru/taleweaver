import { describe, expect, it } from 'vitest'
import type { DraftScenario } from '../api/types'
import { searchNodes } from './search'

const scenario = {
  id: 'hb:scenario/test',
  title: { fr: 'Test' },
  start: 'intro',
  nodes: [
    { id: 'intro', type: 'scene', title: { fr: 'Vesse, au soir' }, narrative: { fr: 'Le colporteur tend une lettre.' } },
    {
      id: 'le-gue',
      type: 'encounter',
      name: { fr: 'Le gué du vieux moulin' },
      events: [{ id: 'e', trigger: { on: 'encounterStart' }, hiddenText: { fr: 'Une lettre flotte sur l’eau.', en: 'A letter' } }],
    },
    { id: 'lettre', type: 'scene', title: { fr: 'Pardon' } },
  ],
} as unknown as DraftScenario

describe('searchNodes', () => {
  it('finds titles first, then ids, then texts at any depth, without accents', () => {
    const hits = searchNodes(scenario, 'lettre', 'fr')

    expect(hits.map((h) => [h.id, h.where])).toEqual([
      ['lettre', 'id'],
      ['intro', 'text'],
      ['le-gue', 'text'],
    ])
    expect(hits[2]?.excerpt).toContain('Une lettre flotte')
  })

  it('needs every word, and folds accents', () => {
    expect(searchNodes(scenario, 'gue moulin', 'fr').map((h) => h.id)).toEqual(['le-gue'])
    expect(searchNodes(scenario, 'gue soir', 'fr')).toEqual([])
    expect(searchNodes(scenario, '  ', 'fr')).toEqual([])
  })
})
