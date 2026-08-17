// Runtime theme / style / preset / direction switching with localStorage persistence.
//
// Knobs, all applied to <html>:
//   • THEME - 'light' | 'dark' | 'system' (the `dark` class; 'system' follows the OS live).
//   • STYLE - the active skin (`style-*` class; structure only).
//   • PRESET - the active color palette (`preset-*` class; 'nova' = no class, the default).
//   • DIRECTION - reading direction (the `dir` attribute).
//   • CHART / RADIUS / FONT / HEADING - token overlays (`chart-*` / `radius-*` / `font-*` /
//     `heading-*` classes; 'default' = no class). The CSS behind each class is the consumer's -
//     Blaizio only swaps the marker class and persists the choice.
//
// Getters fall back to whatever the markup declares when nothing is persisted, so an app's
// index.html defaults (e.g. class="style-ember") stay the single source of truth.
//
// The pre-paint counterpart is ts/boot.ts (bundled as a classic script for <head>), which
// re-applies these persisted values before first paint; keep the storage keys and the applied
// markup in sync between the two files.

import { invokeDotNet } from './core';

const THEME_KEY = 'blaizio-theme';
const STYLE_KEY = 'blaizio-style';
const PRESET_KEY = 'blaizio-preset';
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
  for (const w of watchers.values()) void invokeDotNet(w, 'OnThemeChangedAsync', theme, resolved);
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

/** The active color preset name; 'nova' (the built-in default palette) when none is applied. */
export function getPreset(): string {
  const persisted = read(PRESET_KEY);
  if (persisted) return persisted;
  for (const c of document.documentElement.classList) {
    if (c.startsWith('preset-')) return c.slice('preset-'.length);
  }
  return 'nova';
}

/** Applies a color preset: swaps the `preset-*` class ('nova' = remove it, back to the default). */
export function setPreset(preset: string): void {
  const normalized = preset || 'nova';
  write(PRESET_KEY, normalized);
  const el = document.documentElement;
  for (const c of [...el.classList]) if (c.startsWith('preset-')) el.classList.remove(c);
  if (normalized !== 'nova') el.classList.add('preset-' + normalized);
}

export function getDir(): string {
  return read(DIR_KEY) ?? (document.documentElement.dir === 'rtl' ? 'rtl' : 'ltr');
}

export function setDir(dir: string): void {
  const normalized = dir === 'rtl' ? 'rtl' : 'ltr';
  write(DIR_KEY, normalized);
  document.documentElement.dir = normalized;
}

// Token overlays: each knob swaps one `<prefix>-*` marker class on <html> and persists under
// `blaizio-<prefix>`. 'default' (or '') = no class - the :root tokens win. Same shape as
// preset/nova above; the CSS behind each class ships with the consumer's stylesheet.

function getOverlay(key: string, prefix: string): string {
  const persisted = read(key);
  if (persisted) return persisted;
  for (const c of document.documentElement.classList) {
    if (c.startsWith(prefix)) return c.slice(prefix.length);
  }
  return 'default';
}

function setOverlay(key: string, prefix: string, value: string): void {
  const normalized = value || 'default';
  write(key, normalized);
  const el = document.documentElement;
  for (const c of [...el.classList]) if (c.startsWith(prefix)) el.classList.remove(c);
  if (normalized !== 'default') el.classList.add(prefix + normalized);
}

/** The active chart palette (`chart-*` class); 'default' when none. */
export function getChart(): string {
  return getOverlay('blaizio-chart', 'chart-');
}

export function setChart(chart: string): void {
  setOverlay('blaizio-chart', 'chart-', chart);
}

/** The active radius scale (`radius-*` class); 'default' when none. */
export function getRadius(): string {
  return getOverlay('blaizio-radius', 'radius-');
}

export function setRadius(radius: string): void {
  setOverlay('blaizio-radius', 'radius-', radius);
}

/** The active body font stack (`font-*` class); 'default' when none. */
export function getFont(): string {
  return getOverlay('blaizio-font', 'font-');
}

export function setFont(font: string): void {
  setOverlay('blaizio-font', 'font-', font);
}

/** The active heading font stack (`heading-*` class); 'default' when none. */
export function getHeading(): string {
  return getOverlay('blaizio-heading', 'heading-');
}

export function setHeading(heading: string): void {
  setOverlay('blaizio-heading', 'heading-', heading);
}
