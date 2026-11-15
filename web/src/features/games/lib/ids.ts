/** Readable part of a content id: "um1:initiative/cave-sickle-1-3" → "cave-sickle-1-3", "adventurer:Rook" → "Rook". */
export function shortId(id: string): string {
  if (id.startsWith('adventurer:')) return id.slice('adventurer:'.length)
  const slash = id.lastIndexOf('/')
  return slash >= 0 ? id.slice(slash + 1) : id
}
