/**
 * ImageFill - the browser-side half of the color picker's image surface. It turns a chosen file
 * into an object URL, waits for the bitmap to decode, and rotates it by redrawing through a
 * canvas. Nothing is uploaded and no value is owned here: each call hands C# back a URL, which
 * becomes the picker's paint.
 *
 * Rotation is BAKED into a new bitmap rather than left as a transform, so the URL always points at
 * the image the way it looks - a CSS background cannot rotate its own paint.
 */

/** Object URLs this module minted per preview element, so each one can be revoked when replaced. */
const objectUrls = new WeakMap<HTMLImageElement, string>();

/** Point the preview at a URL and resolve once the bitmap is decoded (false if it never arrives). */
const show = (image: HTMLImageElement, url: string): Promise<boolean> =>
  new Promise<boolean>((resolve) => {
    const done = (ok: boolean): void => {
      image.removeEventListener('load', onLoad);
      image.removeEventListener('error', onError);
      resolve(ok && image.naturalWidth > 0);
    };
    const onLoad = (): void => done(true);
    const onError = (): void => done(false);

    // The load EVENT, not decode(): the preview starts out hidden, and decode() can hang
    // indefinitely for an image that is not in the render tree.
    image.addEventListener('load', onLoad);
    image.addEventListener('error', onError);
    image.src = url;
  });

/** Replace the URL held for a preview, revoking whatever it was showing before. */
const adopt = (image: HTMLImageElement, url: string): void => {
  const previous = objectUrls.get(image);
  if (previous) URL.revokeObjectURL(previous);
  objectUrls.set(image, url);
};

/**
 * Load the first chosen file into the preview. `host` is either the file input itself or an
 * element containing one - a dropzone stretches its own native input over the zone, so the file a
 * user dropped is sitting right there on it. Returns the object URL, or null if unusable.
 */
export const loadImage = async (host: HTMLElement, image: HTMLImageElement): Promise<string | null> => {
  const input =
    host instanceof HTMLInputElement ? host : host.querySelector<HTMLInputElement>('input[type=file]');
  const file = input?.files?.[0];
  if (!file || !file.type.startsWith('image/')) return null;

  const url = URL.createObjectURL(file);
  adopt(image, url);
  return (await show(image, url)) ? url : null;
};

/**
 * Point the decoding image at a URL it is not holding yet - for a surface re-entered with a
 * picture already in the model, so rotating it still has a bitmap to redraw.
 */
export const showImage = async (image: HTMLImageElement, url: string): Promise<boolean> =>
  image.src === url ? image.naturalWidth > 0 : show(image, url);

/** Rotate the preview a quarter turn clockwise, baked into a new bitmap. Returns its URL. */
export const rotateImage = async (image: HTMLImageElement): Promise<string | null> => {
  const w = image.naturalWidth;
  const h = image.naturalHeight;
  if (!w || !h) return null;

  // Swapped dimensions: a quarter turn puts the source's height along the destination's width.
  const canvas = document.createElement('canvas');
  canvas.width = h;
  canvas.height = w;
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;

  ctx.translate(h / 2, w / 2);
  ctx.rotate(Math.PI / 2);
  try {
    ctx.drawImage(image, -w / 2, -h / 2);
  } catch {
    return null; // cross-origin source - the canvas is tainted
  }

  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/png'));
  if (!blob) return null;

  const url = URL.createObjectURL(blob);
  adopt(image, url);
  return (await show(image, url)) ? url : null;
};

/** File extension for the blob types an image surface can hold; anything unknown lands on png. */
const extensionFor = (type: string): string => {
  switch (type) {
    case 'image/jpeg': return 'jpg';
    case 'image/webp': return 'webp';
    case 'image/gif': return 'gif';
    case 'image/avif': return 'avif';
    case 'image/svg+xml': return 'svg';
    default: return 'png';
  }
};

/**
 * Save the preview's current picture to the user's machine AS GRADED: rotation is already baked
 * into the held bitmap, and the tone adjustments are baked here by redrawing through the same
 * filter string the preview renders with (the native functions plus the url(#...) SVG filter,
 * which canvas resolves against the document). With no active filter the original file's bytes
 * go out untouched, keeping its real encoding; a graded image re-encodes as PNG.
 */
export const downloadImage = async (
  image: HTMLImageElement,
  name: string,
  filter: string,
): Promise<boolean> => {
  const url = objectUrls.get(image);
  if (!url) return false;

  const graded = filter !== '' && filter !== 'none';
  if (!graded) {
    const blob = await fetch(url).then((r) => r.blob()).catch(() => null);
    if (!blob) return false;
    save(url, `${name}.${extensionFor(blob.type)}`);
    return true;
  }

  const canvas = document.createElement('canvas');
  canvas.width = image.naturalWidth;
  canvas.height = image.naturalHeight;
  const ctx = canvas.getContext('2d');
  if (!ctx || !canvas.width || !canvas.height) return false;

  ctx.filter = filter;
  try {
    ctx.drawImage(image, 0, 0);
  } catch {
    return false; // cross-origin source - the canvas is tainted
  }

  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/png'));
  if (!blob) return false;

  const download = URL.createObjectURL(blob);
  try {
    save(download, `${name}.png`);
  } finally {
    // The click has synchronously handed the blob to the download; the URL can go at once.
    URL.revokeObjectURL(download);
  }
  return true;
};

/** Trigger a browser download of a URL under the given filename. */
const save = (url: string, filename: string): void => {
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
};

/** Drop the object URL a preview is holding - call from the component's dispose. */
export const releaseImage = (image: HTMLImageElement): void => {
  const url = objectUrls.get(image);
  if (!url) return;
  URL.revokeObjectURL(url);
  objectUrls.delete(image);
};
