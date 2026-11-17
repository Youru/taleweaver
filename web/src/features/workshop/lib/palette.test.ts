import { describe, expect, it } from 'vitest'
import { figureArt, tokenImage, tokenImages, type BoardPieces } from '@/features/table'
import { packOf, tokenChoices, variantLabel } from './palette'

const pieces = {
  tiles: {},
  figures: {},
  families: {},
  tokens: {
    'um1:token/terrain/dangerous': {
      name: { en: 'Dangerous 1X1' },
      category: 'terrain',
      size: '1x1',
      image: 'assets/act1/tokens/dangerous-1x1.jpg',
      variants: ['assets/expansions/be1/tokens/dangerous-1x1-hole.jpg'],
    },
    'um1:token/totem': {
      name: { en: 'Totem' },
      category: 'totem',
      image: 'assets/act1/tokens/totem-blue.jpg',
      colors: { blue: 'assets/act1/tokens/totem-blue.jpg', red: 'assets/act1/tokens/totem-red.jpg' },
    },
    'um1:token/haste': { name: { en: 'Haste' }, category: 'effect', image: 'h.jpg' },
  },
} as unknown as BoardPieces

const choices = (filters = {}) =>
  tokenChoices(
    pieces,
    (type) => (pieces.tokens[type]!.name as { en: string }).en,
    (c) => c,
    (c) => c,
    filters,
  )

describe('the token palette', () => {
  it('offers each colour, then each variant picture, and leaves out what is kept beside the board', () => {
    expect(choices().map((c) => [c.name, c.variant ?? '', c.pack])).toEqual([
      ['Dangerous 1X1', '', 'um1'],
      ['Dangerous 1X1', 'dangerous 1x1 hole', 'be1'],
      ['Totem · blue', '', 'um1'],
      ['Totem · red', '', 'um1'],
    ])
  })

  it('finds a variant by the words of its file name, and filters by pack and category', () => {
    expect(choices({ search: 'hole' }).map((c) => c.image)).toEqual([
      'assets/expansions/be1/tokens/dangerous-1x1-hole.jpg',
    ])
    expect(choices({ pack: 'be1' })).toHaveLength(1)
    expect(choices({ category: 'totem' })).toHaveLength(2)
  })

  it('names the pack of a picture from its folder, and a variant from its file', () => {
    expect(packOf('assets/expansions/be1/tokens/x.jpg', 'um1:token/terrain/dangerous')).toBe('be1')
    expect(packOf(undefined, 'um1:token/exit')).toBe('um1')
    expect(variantLabel('assets/expansions/be1/tokens/bug-creep-2x1-eggs.jpg')).toBe('bug creep 2x1 eggs')
  })
})

describe('the picture of a token', () => {
  const totem = pieces.tokens['um1:token/totem']
  it('is the one chosen on the map, else its colour, else its type', () => {
    expect(tokenImage({ color: 'red', image: 'chosen.jpg' }, totem)).toBe('chosen.jpg')
    expect(tokenImage({ color: 'red' }, totem)).toBe('assets/act1/tokens/totem-red.jpg')
    expect(tokenImage({}, pieces.tokens['um1:token/terrain/dangerous'])).toBe('assets/act1/tokens/dangerous-1x1.jpg')
  })

  it('offers its own picture, its colours and its variants, once each', () => {
    expect(tokenImages(totem)).toEqual(['assets/act1/tokens/totem-blue.jpg', 'assets/act1/tokens/totem-red.jpg'])
    expect(tokenImages(pieces.tokens['um1:token/terrain/dangerous'])).toHaveLength(2)
  })
})

describe('the picture of a figure', () => {
  const figures = {
    ...pieces,
    figures: {
      fragor: { image: 'fragor-standee.png', portrait: 'fragor-round.png' },
      'cave-sickle-warrior': { portrait: 'warrior-round.png', baseSize: '1x1' },
      brute: { image: 'brute-standee.png' },
    },
  } as unknown as BoardPieces

  it('is the round portrait of the books when there is one, else the standee', () => {
    expect(figureArt(figures, 'fragor')).toEqual({ image: 'fragor-round.png', portrait: true })
    expect(figureArt(figures, 'cave-sickle-warrior')).toEqual({ image: 'warrior-round.png', portrait: true })
    expect(figureArt(figures, 'brute')).toEqual({ image: 'brute-standee.png', portrait: false })
    expect(figureArt(figures, 'nobody', 'fragor').image).toBe('fragor-round.png')
    expect(figureArt(figures, 'nobody')).toEqual({ image: undefined, portrait: false })
  })
})
