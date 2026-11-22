import { describe, expect, it } from 'vitest'
import { optionsToSend, urgencyMax } from './options'

describe('options', () => {
  it('sends nothing when every option is off', () => {
    expect(optionsToSend({ tougherMobs: false, urgencyLimit: 3 })).toBeUndefined()
  })

  it('keeps the Urgency limit only with No Player Elimination', () => {
    expect(optionsToSend({ tougherMobs: true, urgencyLimit: 3 })).toEqual({ tougherMobs: true })
    expect(urgencyMax({ urgencyLimit: 3 })).toBe(4)
    expect(urgencyMax({ noPlayerElimination: true, urgencyLimit: 5 })).toBe(5)
  })
})
