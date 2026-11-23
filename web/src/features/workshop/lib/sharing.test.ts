import { describe, expect, it } from 'vitest'
import { exportedFileName, readImported } from './sharing'

describe('sharing a scenario', () => {
  it('names the file after the scenario', () => {
    expect(exportedFileName('homebrew:scenario/meadow-ambush')).toBe('meadow-ambush.taleweaver-scenario.json')
  })

  it('reads an exported scenario back', () => {
    const scenario = {
      id: 'homebrew:scenario/a',
      title: { en: 'A' },
      kind: 'bounty',
      packs: ['demo'],
      start: 'intro',
      nodes: [],
    }

    expect(readImported(JSON.stringify(scenario))).toEqual({ scenario })
  })

  it('refuses what is not a scenario', () => {
    expect(readImported('{ nope')).toEqual({ error: 'notJson' })
    expect(readImported('[]')).toEqual({ error: 'notAScenario' })
    expect(readImported('{ "title": { "en": "A" }, "start": "intro" }')).toEqual({ error: 'notAScenario' })
  })
})
