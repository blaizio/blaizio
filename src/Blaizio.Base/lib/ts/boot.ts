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

// Satisfies isolatedModules; the IIFE bundle strips it, so the output stays a classic script.
export {};
