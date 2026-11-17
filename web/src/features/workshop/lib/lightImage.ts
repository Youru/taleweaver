import { reactive, ref } from 'vue'

/**
 * The JPEGs of the printed pages (tiles, terrain tokens), re-encoded lighter in the browser: the
 * asset files are kept at their best (the table shows them up close), but a PDF copies each one as it
 * is, about 1 MB a tile. At 0.75 a tile weighs four times less, with no difference on paper.
 * The first call starts the work and gives the original; the light copy replaces it once ready.
 * `pendingImages` counts the copies in progress: the print page is "paged" only once it is zero.
 */
const QUALITY = 0.75
const light = reactive(new Map<string, string>())
const started = new Set<string>()
export const pendingImages = ref(0)

export function lightImage(url: string | undefined): string | undefined {
  if (!url || !/\.jpe?g(?:$|\?)/i.test(url) || typeof document === 'undefined') return url
  const done = light.get(url)
  if (done) return done
  if (!started.has(url)) {
    started.add(url)
    pendingImages.value++
    reencode(url)
      .then((data) => light.set(url, data))
      .catch(() => light.set(url, url))
      .finally(() => pendingImages.value--)
  }
  return url
}

async function reencode(url: string): Promise<string> {
  const img = new Image()
  img.src = url
  await img.decode()
  const canvas = document.createElement('canvas')
  canvas.width = img.naturalWidth
  canvas.height = img.naturalHeight
  const g = canvas.getContext('2d')
  if (!g) return url
  g.drawImage(img, 0, 0)
  return canvas.toDataURL('image/jpeg', QUALITY)
}
