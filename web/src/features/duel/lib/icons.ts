import { assetUrl } from '@/shared/api/assets'

const icons = 'assets/templates/discipline-card/icons'

export type SymbolName = 'shield' | 'book' | 'star' | 'skull'
export type StatName = 'defense' | 'armor' | 'hp'

const symbolFiles: Record<SymbolName, string> = {
  shield: 'shield-icon',
  book: 'book-icon',
  star: 'burst-icon',
  skull: 'skull',
}

/** Dice whose faces print their symbols in white (the others print them in black). */
const lightSymbols = new Set(['purple', 'orange', 'red', 'grey', 'black'])

/** A combat symbol, white on dark dice and on the page, black on light dice. */
export function symbolIcon(symbol: SymbolName, onDie?: string): string | undefined {
  const white = onDie === undefined || lightSymbols.has(onDie)
  return assetUrl(`${icons}/combat-symbols/${symbolFiles[symbol]}${white ? '-white' : ''}.png`)
}

export function statIcon(stat: StatName): string | undefined {
  return assetUrl(`${icons}/stat-icons-text/${stat}-icon.png`)
}

export function dieIcon(colour: string): string | undefined {
  return colour === '*' ? undefined : assetUrl(`${icons}/dice/${colour}-dice-icon.png`)
}

/** Text colour that reads on a die of this colour. */
export function inkOn(colour: string): 'light' | 'dark' {
  return lightSymbols.has(colour) ? 'light' : 'dark'
}
