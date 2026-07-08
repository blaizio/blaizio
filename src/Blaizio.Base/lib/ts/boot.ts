// Pre-paint bootstrap: re-applies the persisted theme / style / direction before first paint so
// a reload doesn't flash the markup defaults. Bundled as a CLASSIC script (IIFE, not ESM - see
// lib/build.mjs) so consumers load it synchronously in <head>:
//
//   <script src="_content/Blaizio.Base/dist/boot.js"></script>
//
// This is the one piece of Blaizio JS that runs before Blazor boots; everything else goes
// through ESM modules. Runtime switching (persist + apply) lives in ts/theme.ts - keep the
// storage keys and the applied markup in sync between the two files. When nothing is persisted
// the markup defaults (e.g. class="style-ember") are left untouched.

const el = document.documentElement;
let theme: string | null = null;
let style: string | null = null;
let dir: string | null = null;
try {
  theme = localStorage.getItem('blaizio-theme');
  style = localStorage.getItem('blaizio-style');
  dir = localStorage.getItem('blaizio-dir');
} catch {
  // storage unavailable (privacy mode) - keep the markup defaults
}
if (theme) {
  el.classList.toggle('dark', theme === 'dark');
  if (theme === 'light' || theme === 'dark') el.removeAttribute('data-theme');
  else el.setAttribute('data-theme', theme);
} else if (matchMedia('(prefers-color-scheme: dark)').matches) {
  // No explicit choice yet - follow the OS. Once the user picks a theme, the persisted value
  // wins on every future load.
  el.classList.add('dark');
}
if (style) {
  for (const c of [...el.classList]) if (c.startsWith('style-')) el.classList.remove(c);
  el.classList.add('style-' + style);
}
if (dir) el.dir = dir === 'rtl' ? 'rtl' : 'ltr';

// Satisfies isolatedModules; the IIFE bundle strips it, so the output stays a classic script.
export {};
