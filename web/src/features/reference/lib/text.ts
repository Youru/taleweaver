/** Lower case, no accent, words separated by one space: what the search compares. */
export function normalize(text: string): string {
  return text
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .toLowerCase()
    .replace(/œ/g, 'oe')
    .replace(/[^a-z0-9]+/g, ' ')
    .trim()
}

/** A word without its plural. */
export function singular(word: string): string {
  if (word.length > 4 && word.endsWith('ies')) return `${word.slice(0, -3)}y`
  return word.length > 3 && /[sx]$/.test(word) && !word.endsWith('ss') ? word.slice(0, -1) : word
}

/**
 * Where a word or a phrase starts in a text. A word also finds what it begins ("attack" finds "attacks"), except a
 * short one: "sp" must not find "space".
 */
export function termPattern(term: string): RegExp {
  const words = term
    .split(' ')
    .filter(Boolean)
    .map((word) => (word.length <= 3 ? `${word}s?(?![a-z0-9])` : `${word}[a-z0-9]*`))
  return new RegExp(`(?<![a-z0-9])${words.join('[^a-z0-9]+')}`, 'gi')
}

/** How many letters to change to turn a word into another, counted up to `most` + 1. */
export function distance(from: string, to: string, most: number): number {
  if (Math.abs(from.length - to.length) > most) return most + 1
  let before: number[] = []
  let previous = Array.from({ length: to.length + 1 }, (_, index) => index)
  for (let i = 1; i <= from.length; i++) {
    const row = [i]
    for (let j = 1; j <= to.length; j++) {
      let cost = Math.min(previous[j]! + 1, row[j - 1]! + 1, previous[j - 1]! + (from[i - 1] === to[j - 1] ? 0 : 1))
      // Two letters typed in the wrong order are one mistake
      if (i > 1 && j > 1 && from[i - 1] === to[j - 2] && from[i - 2] === to[j - 1])
        cost = Math.min(cost, before[j - 2]! + 1)
      row.push(cost)
    }
    if (Math.min(...row) > most) return most + 1
    before = previous
    previous = row
  }
  return previous[to.length]!
}
