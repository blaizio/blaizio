// Settled-state probe for <img>. Blazor attaches @onload/@onerror during render, so an image
// served from cache (or present in prerendered HTML) can finish loading before the handlers
// exist - the event fires into the void and C# waits forever. BaseImageImg calls status() after
// render to pick up loads/errors that already happened.
//
// naturalWidth > 0 distinguishes a successful decode from a settled failure (a broken image is
// "complete" with zero intrinsic width).

export function status(el: HTMLImageElement | null): 'pending' | 'loaded' | 'error' {
  if (!el || !el.complete) return 'pending';
  return el.naturalWidth > 0 ? 'loaded' : 'error';
}
