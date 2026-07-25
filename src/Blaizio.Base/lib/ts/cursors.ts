/**
 * Cursors - turns SVG cursor art into something every engine will actually draw.
 *
 * Chromium does not support SVG images in `cursor: url(...)`: the declaration parses, the image is
 * ignored, and the browser silently falls back to the keyword after the comma. So the picture is
 * rasterised here once, through a canvas, and handed back as a PNG data URL - which every engine
 * does support. The result is written straight onto an element as custom properties, so the sheets
 * can keep reading `cursor: var(--...)` and know nothing about any of this.
 */

interface CursorSpec {
  /** Custom property to set, e.g. `--bz-color-cursor-stop`. */
  name: string;
  /** The source image, typically an SVG data URI. */
  url: string;
  /** Hotspot x, in image pixels. */
  x: number;
  /** Hotspot y, in image pixels. */
  y: number;
}

/** Rasterise one image to a PNG data URL at its own size, or null when it cannot be drawn. */
const rasterise = async (url: string): Promise<string | null> => {
  const image = new Image();
  const loaded = await new Promise<boolean>((resolve) => {
    image.onload = (): void => resolve(true);
    image.onerror = (): void => resolve(false);
    image.src = url;
  });
  if (!loaded) return null;

  // An SVG without intrinsic dimensions decodes to 0x0 - nothing to rasterise.
  const width = image.naturalWidth;
  const height = image.naturalHeight;
  if (!width || !height) return null;

  const canvas = document.createElement('canvas');
  canvas.width = width;
  canvas.height = height;
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;

  ctx.drawImage(image, 0, 0, width, height);
  try {
    return canvas.toDataURL('image/png');
  } catch {
    return null; // tainted canvas - cannot happen for a data: URI, but never throw at a caller
  }
};

/**
 * Rasterise each spec and write it onto `root` as its custom property. A spec that cannot be drawn
 * is skipped, leaving whatever the element already carries - so the SVG value stays as the
 * fallback for engines that do render it.
 */
export const applyCursors = async (root: HTMLElement, specs: CursorSpec[]): Promise<void> => {
  for (const spec of specs) {
    const png = await rasterise(spec.url);
    if (png) root.style.setProperty(spec.name, `url(${png}) ${spec.x} ${spec.y}`);
  }
};
