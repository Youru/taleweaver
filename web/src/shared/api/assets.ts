import { inject, type InjectionKey } from 'vue'
import { currentScreenToken } from '@/shared/lib/screen'

/**
 * URL of a game image. The catalogue gives paths from the repository root ("assets/act1/tiles/um01.jpg");
 * the API serves the assets folder under /api/assets. The images stay on the user's disk.
 */
export function assetUrl(path: string | undefined | null, width?: AssetWidth): string | undefined {
  if (!path) return undefined
  const relative = path.replace(/^\/?assets\//, '')
  const url = `/api/assets/${relative.split('/').map(encodeURIComponent).join('/')}`
  const query = new URLSearchParams()
  if (width) query.set('w', String(width))
  // An <img> sends no header: the GM's tablet proves itself in the address, as for the hub
  const screen = currentScreenToken()
  if (screen) query.set('screen_token', screen)
  return query.size ? `${url}?${query}` : url
}

/**
 * The widths the API resizes to (room-spec §10.3): a thumbnail, a phone, a large screen. The phones ask for
 * these; the GM's screens keep the originals.
 */
export type AssetWidth = 160 | 320 | 640 | 1280

/**
 * Width of the small pictures (cards in a grid, the initiative track) for the components below: the phone's
 * layout provides 320, elsewhere the originals are served.
 */
export const thumbnailWidthKey: InjectionKey<AssetWidth> = Symbol('thumbnailWidth')

export function useThumbnailWidth(): AssetWidth | undefined {
  return inject(thumbnailWidthKey, undefined)
}
