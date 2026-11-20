import { describe, expect, it } from 'vitest'
import type { LogEntry } from '../api/types'
import { encounterOver, storyPage } from './storyPage'

const codes = (log: LogEntry[]) => storyPage(log).map((e) => e.code)

describe('storyPage', () => {
  it('shows the last scene only, not the ones before', () => {
    expect(codes([{ code: 'scene' }, { code: 'answer' }, { code: 'scene' }])).toEqual(['scene'])
  })

  it('keeps what follows the scene in the story, and leaves the bookkeeping to the journal', () => {
    const log: LogEntry[] = [
      { code: 'scene' },
      { code: 'answer' },
      { code: 'setVar' },
      { code: 'hiddenText' },
      { code: 'gainGold' },
      { code: 'manualAction' },
    ]

    expect(codes(log)).toEqual(['scene', 'hiddenText', 'gainGold', 'manualAction'])
  })

  it('opens on the end of the encounter just played, with its reward', () => {
    const log: LogEntry[] = [
      { code: 'scene' },
      { code: 'encounter' },
      { code: 'roundStart' },
      { code: 'urgency' },
      { code: 'encounterEnd' },
      { code: 'reward' },
      { code: 'answer' },
      { code: 'addVar' },
      { code: 'restore' },
    ]

    expect(codes(log)).toEqual(['encounterEnd', 'reward'])
  })

  it('shows the opening texts when no scene has started yet', () => {
    expect(codes([{ code: 'text' }, { code: 'answer' }])).toEqual(['text'])
  })

  it('knows an encounter is over once an end condition is reached, until the next one starts', () => {
    expect(encounterOver([{ code: 'encounter' }, { code: 'roundStart' }])).toBe(false)
    expect(encounterOver([{ code: 'encounter' }, { code: 'encounterEnd' }, { code: 'answer' }])).toBe(true)
    expect(encounterOver([{ code: 'encounter' }, { code: 'encounterEnd' }, { code: 'encounter' }])).toBe(false)
  })
})
