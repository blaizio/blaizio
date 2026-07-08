// Runtime theme / style / direction switching with localStorage persistence.
//
// Three knobs, all applied to <html>:
//   • THEME - light/dark (the `dark` class) or a named color theme (`data-theme` attribute).
//   • STYLE - the active skin (`style-*` class).
//   • DIRECTION - reading direction (the `dir` attribute).
//
// Getters fall back to whatever the markup declares when nothing is persisted, so an app's
// index.html defaults (e.g. class="style-ember") stay the single source of truth.
//
// The pre-paint counterpart is ts/boot.ts (bundled as a classic script for <head>), which
// re-applies these persisted values before first paint; keep the storage keys and the applied
// markup in sync between the two files.

const THEME_KEY = 'blaizio-theme';
const STYLE_KEY = 'blaizio-style';
const DIR_KEY = 'blaizio-dir';

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null; // storage unavailable (privacy mode) - markup defaults win
  }
}

function write(key: string, value: string): void {
  try {
    localStorage.setItem(key, value);
  } catch {
    // storage unavailable - the change still applies, it just won't survive a reload
  }
}

export function getTheme(): string {
  const el = document.documentElement;
  return read(THEME_KEY) ?? (el.classList.contains('dark') ? 'dark' : (el.getAttribute('data-theme') ?? 'light'));
}

export function setTheme(theme: string): void {
  write(THEME_KEY, theme);
  const el = document.documentElement;
  el.classList.toggle('dark', theme === 'dark');
  if (theme === 'light' || theme === 'dark') el.removeAttribute('data-theme');
  else el.setAttribute('data-theme', theme);
}

export function getStyle(): string {
  const persisted = read(STYLE_KEY);
  if (persisted) return persisted;
  for (const c of document.documentElement.classList) {
    if (c.startsWith('style-')) return c.slice('style-'.length);
  }
  return '';
}

export function setStyle(style: string): void {
  write(STYLE_KEY, style);
  const el = document.documentElement;
  for (const c of [...el.classList]) if (c.startsWith('style-')) el.classList.remove(c);
  el.classList.add('style-' + style);
}

export function getDir(): string {
  return read(DIR_KEY) ?? (document.documentElement.dir === 'rtl' ? 'rtl' : 'ltr');
}

export function setDir(dir: string): void {
  const normalized = dir === 'rtl' ? 'rtl' : 'ltr';
  write(DIR_KEY, normalized);
  document.documentElement.dir = normalized;
}
