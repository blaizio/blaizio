// Runtime theme / style / direction switching with localStorage persistence.
//
// Three knobs, all applied to <html>:
//   • THEME - 'light' | 'dark' | 'system' (the `dark` class; 'system' follows the OS live).
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

const prefersDark = matchMedia('(prefers-color-scheme: dark)');

function apply(theme: string): void {
  const dark = theme === 'dark' || (theme !== 'light' && prefersDark.matches);
  document.documentElement.classList.toggle('dark', dark);
}

/** The persisted preference: 'light' | 'dark' | 'system' (the default when nothing is stored). */
export function getTheme(): string {
  const persisted = read(THEME_KEY);
  return persisted === 'light' || persisted === 'dark' ? persisted : 'system';
}

/** What is actually on screen right now: 'light' | 'dark' ('system' resolved against the OS). */
export function getResolvedTheme(): string {
  return document.documentElement.classList.contains('dark') ? 'dark' : 'light';
}

export function setTheme(theme: string): void {
  const normalized = theme === 'light' || theme === 'dark' ? theme : 'system';
  write(THEME_KEY, normalized);
  apply(normalized);
  notify();
}

// While in system mode the OS preference is live - re-resolve when it flips.
prefersDark.addEventListener('change', () => {
  if (getTheme() === 'system') {
    apply('system');
    notify();
  }
});

// Theme-change subscription for Blazor components (theme switchers rendered in several places
// stay in sync: every setTheme / OS flip notifies each watcher). Id-based so a component can
// unsubscribe with the handle watchTheme returned.
interface ThemeWatcher {
  invokeMethodAsync(method: string, theme: string, resolved: string): Promise<void>;
}

const watchers = new Map<number, ThemeWatcher>();
let nextWatchId = 1;

function notify(): void {
  const theme = getTheme();
  const resolved = getResolvedTheme();
  for (const w of watchers.values()) void w.invokeMethodAsync('OnThemeChangedAsync', theme, resolved);
}

/** Subscribe a DotNetObjectReference; its OnThemeChangedAsync(theme, resolved) is invoked on every change. */
export function watchTheme(watcher: ThemeWatcher): number {
  watchers.set(nextWatchId, watcher);
  return nextWatchId++;
}

export function unwatchTheme(id: number): void {
  watchers.delete(id);
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
