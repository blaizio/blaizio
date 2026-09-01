// Pre-paint bootstrap: re-applies the persisted theme / style / direction before first paint so
// a reload doesn't flash the markup defaults. Bundled as a CLASSIC script (IIFE, not ESM - see
// lib/build.mjs) so consumers load it synchronously in <head>:
//
//   <script src="_content/blaizio.base/dist/boot.js"></script>
//
// This is the one piece of Blaizio JS that runs before Blazor boots; everything else goes
// through ESM modules. Runtime switching (persist + apply) lives in ts/theme.ts - keep the
// storage keys and the applied markup in sync between the two files. When nothing is persisted
// the markup defaults (e.g. class="style-ember") are left untouched.

const el = document.documentElement;
let theme: string | null = null;
let style: string | null = null;
let preset: string | null = null;
let dir: string | null = null;
try {
  theme = localStorage.getItem('blaizio-theme');
  style = localStorage.getItem('blaizio-style');
  preset = localStorage.getItem('blaizio-preset');
  dir = localStorage.getItem('blaizio-dir');
} catch {
  // storage unavailable (privacy mode) - keep the markup defaults
}
// 'light' | 'dark' are explicit choices; anything else ('system', nothing persisted yet, or a
// legacy value) resolves against the OS preference.
el.classList.toggle('dark', theme === 'dark' || (theme !== 'light' && matchMedia('(prefers-color-scheme: dark)').matches));
if (style) {
  for (const c of [...el.classList]) if (c.startsWith('style-')) el.classList.remove(c);
  el.classList.add('style-' + style);
}
// A persisted 'nova' (the default palette) strips any markup preset class rather than adding one.
if (preset) {
  for (const c of [...el.classList]) if (c.startsWith('preset-')) el.classList.remove(c);
  if (preset !== 'nova') el.classList.add('preset-' + preset);
}
if (dir) el.dir = dir === 'rtl' ? 'rtl' : 'ltr';

// Token overlays (chart palette, radius scale, body/heading font) - same shape as preset above:
// a persisted 'default' strips the marker class, anything else swaps it in. Keep the storage
// keys and prefixes in sync with ts/theme.ts.
for (const [key, prefix] of [
  ['blaizio-chart', 'chart-'],
  ['blaizio-radius', 'radius-'],
  ['blaizio-font', 'font-'],
  ['blaizio-heading', 'heading-'],
] as const) {
  let value: string | null = null;
  try {
    value = localStorage.getItem(key);
  } catch {
    // storage unavailable - keep the markup defaults
  }
  if (!value) continue;
  for (const c of [...el.classList]) if (c.startsWith(prefix)) el.classList.remove(c);
  if (value !== 'default') el.classList.add(prefix + value);
}

// Floating-surface module warmup. Dialogs, menus and popovers lazy-import their interop modules
// (presence / positioning / portal / ...) on first open; on a cold load that first import pays
// fetch + parse mid-open, and the entry animation plays while the surface is still hidden
// (popovers) or restarts when the portal reparents the node (dialogs) - a visible first-open
// stutter. Warming the browser's module cache after page load (idle-time, so Blazor's own boot
// wins the bandwidth race) makes the later interop import() resolve from the module map
// instantly. The warmup URL and the interop path normalize to the same URL, so it IS the same
// module instance. Prefetch only: failures are swallowed - the real import on first open
// surfaces any genuine problem - and users on data-saver connections are left alone.
const bootSrc = (document.currentScript as HTMLScriptElement | null)?.src;
const saveData = (navigator as Navigator & { connection?: { saveData?: boolean } }).connection?.saveData;
if (bootSrc && !saveData) {
  const warm = () => {
    for (const name of ['presence', 'positioning', 'portal', 'dismissableLayer', 'focusScope', 'scrollLock', 'menu']) {
      import(new URL(`${name}.js`, bootSrc).href).catch(() => {
        // Cold stays cold; the on-open import reports for real.
      });
    }
  };
  const idle: (cb: () => void) => void =
    'requestIdleCallback' in window ? (cb) => requestIdleCallback(cb) : (cb) => void setTimeout(cb, 300);
  if (document.readyState === 'complete') idle(warm);
  else window.addEventListener('load', () => idle(warm), { once: true });
}

// Satisfies isolatedModules; the IIFE bundle strips it, so the output stays a classic script.
export {};
