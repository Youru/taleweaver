/**
 * URL of a game image. The catalogue gives paths from the repository root ("assets/act1/tiles/um01.jpg");
 * the API serves the assets folder under /api/assets. The images stay on the user's disk.
 */
export function assetUrl(path: string | undefined | null): string | undefined {
  if (!path) return undefined
  const relative = path.replace(/^\/?assets\//, '')
  return `/api/assets/${relative.split('/').map(encodeURIComponent).join('/')}`
}
