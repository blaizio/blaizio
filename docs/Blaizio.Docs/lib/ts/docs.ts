// Blaizio.Docs interop module - imported once by the DocsJs service. No window globals;
// everything the app needs from the browser goes through here.
//
// SOURCE of wwwroot/js/docs.js: bundled by esbuild (`pnpm build:js` / the csproj's BuildDocsJs
// target). The ../_content import is a RUNTIME path (relative to wwwroot/js/) and is marked
// external in the bundle, so it resolves in the browser against Blazor's static web assets -
// which is also why the TS language service cannot resolve it here.

// Theme / style / preset / direction / token-overlay switching comes from Blaizio.Base's theme
// module (persisted to localStorage; the pre-paint counterpart is its dist/boot.js, loaded in
// index.html <head>).
export {
    getTheme, setTheme, getStyle, setStyle, getPreset, setPreset, getDir, setDir,
    getChart, setChart, getRadius, setRadius, getFont, setFont, getHeading, setHeading,
} from '../_content/blaizio.base/dist/theme.js';

import { getTheme as getThemePref, setTheme as applyTheme } from '../_content/blaizio.base/dist/theme.js';

// "D" cycles the theme PREFERENCE app-wide: light -> dark -> system -> light, mirroring the
// header's BzThemeToggle (ShowSystem) so the key and the button walk the same three stops.
// Installed once, on first import - same pattern as the scroll listener below. setTheme notifies
// theme.js watchers, so every BzThemeSwitcher on the page stays in sync. Skipped while typing or
// when a modifier is held.
document.addEventListener('keydown', (e: KeyboardEvent) => {
    if (e.key !== 'd' && e.key !== 'D') return;
    if (e.ctrlKey || e.metaKey || e.altKey || e.shiftKey) return;
    const t = e.target as HTMLElement | null;
    if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
    const pref = getThemePref();
    applyTheme(pref === 'light' ? 'dark' : pref === 'dark' ? 'system' : 'light');
});

export function copy(text: string): void {
    navigator.clipboard?.writeText(text).catch(() => { });
}

// Lazily inject a Google Fonts css2 stylesheet for the /create webfont knobs. The overlay classes
// (font-*/heading-* in create-overlays.css) only name the family; this actually loads it - once
// per href, deduplicated across pages and picks. The C# side (FontCatalog) owns the name→URL
// mapping; this just takes the finished href.
const loadedFonts = new Set<string>();
export function loadWebFont(href: string): void {
    if (!href || loadedFonts.has(href)) return;
    loadedFonts.add(href);
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = href;
    document.head.appendChild(link);
}

// Activity scrollbars + edge fades: on any [data-scroll-activity] element, reveal the scrollbar
// only WHILE it is being scrolled (fading out ~900ms after), and record whether the content is
// currently at the top / bottom so the `scroll-fade-y` mask only fades an edge that actually has
// content beyond it (so the first row stays crisp when scrolled to the top). Uses a capturing
// listener (installed once, on first import) so it also covers elements Blazor mounts later
// (scroll doesn't bubble).

// Mark which edges are scrolled-away (1px slack absorbs sub-pixel rounding).
function fadeEdges(el: HTMLElement): void {
    el.dataset.atTop = String(el.scrollTop <= 1);
    el.dataset.atBottom = String(el.scrollTop + el.clientHeight >= el.scrollHeight - 1);
}

const scrollTimers = new WeakMap<HTMLElement, ReturnType<typeof setTimeout>>();
document.addEventListener('scroll', (e) => {
    const el = e.target as HTMLElement | null;
    if (!el || el.nodeType !== 1 || !el.matches || !el.matches('[data-scroll-activity]')) return;
    el.classList.add('is-scrolling');
    fadeEdges(el);
    clearTimeout(scrollTimers.get(el));
    scrollTimers.set(el, setTimeout(() => el.classList.remove('is-scrolling'), 900));
}, true);

// Re-measure every activity element (called by Blazor after the nav list (re)renders).
export function scrollFadeRefresh(): void {
    document.querySelectorAll<HTMLElement>('[data-scroll-activity]').forEach(fadeEdges);
}

// Sidebar grouping preference: flat component list (default) or grouped by category. Persisted
// like the theme picks; NavMenu reads it after first render and the toggle writes it.
const NAV_GROUPED_KEY = 'blaizio-docs-nav-grouped';
export function getNavGrouped(): boolean {
    try { return localStorage.getItem(NAV_GROUPED_KEY) === 'true'; } catch { return false; }
}
export function setNavGrouped(grouped: boolean): void {
    try { localStorage.setItem(NAV_GROUPED_KEY, String(grouped)); } catch { }
}

// Sliding active-page indicator for the sidebar nav. Each [data-nav-list] holds one
// [data-nav-indicator]; we move it to the active row (a[aria-current=page]) so it animates from
// the old row to the new one via its CSS transition. NavMenu calls navPosition() after each
// render (i.e. after every navigation / tab switch); a ResizeObserver keeps it aligned on
// layout changes.
function placeIndicator(list: HTMLElement): void {
    const ind = list.querySelector<HTMLElement>('[data-nav-indicator]');
    if (!ind) return;
    const active = list.querySelector<HTMLElement>('a[aria-current="page"]');
    if (!active) { ind.dataset.show = 'false'; return; }
    const lr = list.getBoundingClientRect(), ar = active.getBoundingClientRect();
    ind.style.setProperty('--nav-ind-y', (ar.top - lr.top) + 'px');
    ind.style.setProperty('--nav-ind-h', ar.height + 'px');
    ind.dataset.show = 'true';
}

let navResizeObserver: ResizeObserver | undefined;
export function navPosition(): void {
    const lists = document.querySelectorAll<HTMLElement>('[data-nav-list]');
    if (!navResizeObserver) { navResizeObserver = new ResizeObserver(() => lists.forEach(placeIndicator)); }
    lists.forEach(l => { placeIndicator(l); try { navResizeObserver!.observe(l); } catch { } });
}

// Scroll the active row into view inside the sidebar's own scroller - used once on load, so a
// deep link (/components/tooltip) opens with its nav row visible.
export function navReveal(): void {
    // Last match, not first: on a component page BOTH the "Components" guide link (a prefix match)
    // and the component's own row carry aria-current - the row is the one worth revealing.
    const anchors = document.querySelectorAll<HTMLElement>('[data-scroll-activity] a[aria-current="page"]');
    const active = anchors[anchors.length - 1];
    if (!active) return;
    const scroller = active.closest<HTMLElement>('[data-scroll-activity]');
    if (!scroller) return;
    const sr = scroller.getBoundingClientRect(), ar = active.getBoundingClientRect();
    if (ar.top < sr.top || ar.bottom > sr.bottom) {
        scroller.scrollTop += ar.top - sr.top - (sr.height - ar.height) / 2;
    }
}
