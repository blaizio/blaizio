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
import { COMMUNITY_ID, COMMUNITY_KEY, TOKENS_ID, TOKENS_KEY } from './storageKeys';

export {
    getTheme, setTheme, getStyle, setStyle, getPreset, setPreset, getDir, setDir,
    getChart, setChart, getRadius, setRadius, getFont, setFont, getHeading, setHeading,
} from '../_content/blaizio.base/dist/theme.js';

import { setTheme as applyTheme } from '../_content/blaizio.base/dist/theme.js';

// "D" toggles the theme app-wide: light <-> dark, mirroring the header's BzThemeToggle (which no
// longer offers the system stop) so the key and the button behave identically. Toggles off the
// RESOLVED mode - a persisted "system" preference flips to the opposite of what is on screen.
// Installed once, on first import - same pattern as the scroll listener below. setTheme notifies
// theme.js watchers, so every BzThemeSwitcher on the page stays in sync. Skipped while typing or
// when a modifier is held.
document.addEventListener('keydown', (e: KeyboardEvent) => {
    if (e.key !== 'd' && e.key !== 'D') return;
    if (e.ctrlKey || e.metaKey || e.altKey || e.shiftKey) return;
    const t = e.target as HTMLElement | null;
    if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
    applyTheme(document.documentElement.classList.contains('dark') ? 'light' : 'dark');
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

// Community themes (/community): token overrides shipped as data (a JSON of :root/.dark values),
// not as preset-* classes, so any community-authored palette can apply without a rebuild. The CSS
// is injected as a <style> appended to <head> - same-specificity rules win by source order, so it
// overrides the stylesheet's :root/.dark AND any preset-* class. Persisted as {name, css}; the
// pre-paint counterpart (prepaint.ts, a classic script next to boot.js in index.html) re-injects
// it before first render so a reload doesn't flash the stock palette.

export function getCommunityTheme(): string {
    try { return JSON.parse(localStorage.getItem(COMMUNITY_KEY) ?? 'null')?.name ?? ''; } catch { return ''; }
}

export function setCommunityTheme(name: string, css: string): void {
    try { localStorage.setItem(COMMUNITY_KEY, JSON.stringify({ name, css })); } catch { }
    let el = document.getElementById(COMMUNITY_ID);
    if (!el) {
        el = document.createElement('style');
        el.id = COMMUNITY_ID;
        document.head.appendChild(el);
    }
    el.textContent = css;
}

export function clearCommunityTheme(): void {
    try { localStorage.removeItem(COMMUNITY_KEY); } catch { }
    document.getElementById(COMMUNITY_ID)?.remove();
}

// Theme token overrides (/themes direct editing): the composer's edited tokens as a generated
// :root:not(.dark)/:root.dark stylesheet (built in C#, ThemeTokens.BuildCss - !important so it
// outranks preset-* classes at any source order). Same shape as the community mechanism: injected
// style + localStorage + the pre-paint re-inject in prepaint.ts.

export function getTokenOverrides(): string {
    try { return localStorage.getItem(TOKENS_KEY) ?? ''; } catch { return ''; }
}

export function setTokenOverrides(css: string): void {
    if (!css) { clearTokenOverrides(); return; }
    try { localStorage.setItem(TOKENS_KEY, css); } catch { }
    let el = document.getElementById(TOKENS_ID);
    if (!el) {
        el = document.createElement('style');
        el.id = TOKENS_ID;
        document.head.appendChild(el);
    }
    el.textContent = css;
}

export function clearTokenOverrides(): void {
    try { localStorage.removeItem(TOKENS_KEY); } catch { }
    document.getElementById(TOKENS_ID)?.remove();
}

/** Whether the document currently renders dark (the resolved mode, not the preference). */
export function isDark(): boolean {
    return document.documentElement.classList.contains('dark');
}

/** The live computed value of a theme custom property (e.g. "primary"), '' when undefined. */
export function getTokenValue(name: string): string {
    return getComputedStyle(document.documentElement).getPropertyValue('--' + name).trim();
}

/** Bulk form of getTokenValue - one interop call refreshes the dock's whole swatch strip. */
export function getTokenValues(names: string[]): Record<string, string> {
    const cs = getComputedStyle(document.documentElement);
    return Object.fromEntries(names.map(n => [n, cs.getPropertyValue('--' + n).trim()]));
}

/**
 * getTokenValues as the OTHER mode would resolve it - what /themes reads when the token popover
 * edits dark while the site renders light (or the reverse). The class flips, the values are read
 * and the class is restored inside ONE synchronous task: the browser only paints between tasks, so
 * nothing on screen ever shows the other mode, and no transition runs (the frame's style change
 * event sees the class back where it started).
 */
export function getTokenValuesInMode(names: string[], dark: boolean): Record<string, string> {
    const html = document.documentElement;
    const was = html.classList.contains('dark');
    if (was === dark) return getTokenValues(names);
    html.classList.toggle('dark', dark);
    try {
        return getTokenValues(names);
    } finally {
        html.classList.toggle('dark', was);
    }
}

// Activity scrollbars: on any [data-scroll-activity] element, reveal the scrollbar only WHILE it is
// being scrolled (fading out ~900ms after). Uses a capturing listener (installed once, on first
// import) so it also covers elements Blazor mounts later (scroll doesn't bubble). The edge fades
// that used to be measured here are now the `scroll-fade-*` utilities, which track scroll position
// in CSS.
const scrollTimers = new WeakMap<HTMLElement, ReturnType<typeof setTimeout>>();
document.addEventListener('scroll', (e) => {
    const el = e.target as HTMLElement | null;
    if (!el || el.nodeType !== 1 || !el.matches || !el.matches('[data-scroll-activity]')) return;
    el.classList.add('is-scrolling');
    clearTimeout(scrollTimers.get(el));
    scrollTimers.set(el, setTimeout(() => el.classList.remove('is-scrolling'), 900));
}, true);

// Floating horizontal scrollbar for wide tables. A table's native horizontal scrollbar sits at
// the container's bottom edge, which can be far below the fold - unusable without scrolling the
// page first. For every table scroll container that (a) overflows horizontally and (b) has its
// bottom edge below the viewport while its top is above it, a thin fixed proxy scrollbar is shown
// at the viewport's bottom edge, kept in perfect sync with the container's scrollLeft both ways.
// Installed once on import; a MutationObserver keeps the set current as Blazor swaps pages.
const hscrollProxies = new Map<HTMLElement, HTMLElement>();
let hscrollSyncing = false;

function hscrollUpdate(): void {
    const containers = document.querySelectorAll<HTMLElement>('main [data-slot="table-container"]');
    const seen = new Set<HTMLElement>();

    containers.forEach(el => {
        seen.add(el);
        const rect = el.getBoundingClientRect();
        const overflows = el.scrollWidth > el.clientWidth + 1;
        // Show while the table is on screen but its own scrollbar (the bottom edge) is not.
        const wanted = overflows && rect.top < innerHeight - 20 && rect.bottom > innerHeight;

        let proxy = hscrollProxies.get(el);
        if (!wanted) {
            if (proxy) { proxy.remove(); hscrollProxies.delete(el); }
            return;
        }

        if (!proxy) {
            proxy = document.createElement('div');
            proxy.className = 'bz-hscroll-proxy';
            proxy.setAttribute('aria-hidden', 'true');
            proxy.appendChild(document.createElement('div'));
            proxy.addEventListener('scroll', () => {
                if (hscrollSyncing) return;
                hscrollSyncing = true;
                el.scrollLeft = proxy!.scrollLeft;
                hscrollSyncing = false;
            });
            document.body.appendChild(proxy);
            hscrollProxies.set(el, proxy);
        }

        const spacer = proxy.firstElementChild as HTMLElement;
        spacer.style.width = el.scrollWidth + 'px';
        spacer.style.height = '1px';
        proxy.style.left = rect.left + 'px';
        proxy.style.width = rect.width + 'px';
        if (!hscrollSyncing && proxy.scrollLeft !== el.scrollLeft) {
            hscrollSyncing = true;
            proxy.scrollLeft = el.scrollLeft;
            hscrollSyncing = false;
        }
    });

    // Containers that left the DOM take their proxies with them.
    hscrollProxies.forEach((proxy, el) => {
        if (!seen.has(el) || !el.isConnected) { proxy.remove(); hscrollProxies.delete(el); }
    });
}

let hscrollQueued = false;
function hscrollSchedule(): void {
    if (hscrollQueued) return;
    hscrollQueued = true;
    // rAF for scroll-smoothness, raced against a timeout: in a hidden or throttled tab the
    // animation clock can stall entirely, and the proxies must still track the layout.
    const run = () => {
        if (!hscrollQueued) return;
        hscrollQueued = false;
        hscrollUpdate();
    };
    requestAnimationFrame(run);
    setTimeout(run, 80);
}

// Capturing scroll covers both the page and the containers themselves (scroll doesn't bubble);
// the mutation observer covers Blazor page swaps and late-rendered tables.
document.addEventListener('scroll', hscrollSchedule, true);
window.addEventListener('resize', hscrollSchedule);
new MutationObserver(hscrollSchedule).observe(document.documentElement, { childList: true, subtree: true });

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
    // [data-nav-scroller] is the panel's own marker: it used to be found through the scrollbar
    // behaviour's attribute, which quietly broke the reveal the day the panel changed scrollbars.
    const anchors = document.querySelectorAll<HTMLElement>('[data-nav-scroller] a[aria-current="page"]');
    const active = anchors[anchors.length - 1];
    if (!active) return;
    const scroller = active.closest<HTMLElement>('[data-nav-scroller]');
    if (!scroller) return;
    const sr = scroller.getBoundingClientRect(), ar = active.getBoundingClientRect();
    if (ar.top < sr.top || ar.bottom > sr.bottom) {
        scroller.scrollTop += ar.top - sr.top - (sr.height - ar.height) / 2;
    }
}

// ---- Inspect mode (the Demo shell's slot target map) --------------------------------------------
// Outlines every [data-slot] part inside a demo preview and reports the hovered one to C#, which
// maps the slot to its component type / parameters. Interaction stays LIVE: a dialog, menu or
// select opens as usual, and its parts are inspectable too - floating surfaces are portaled to
// document.body (portal.ts), so ownership is traced back through the placeholder each portaled
// element keeps, recursively (a select inside a dialog). Escape leaves Inspect, unless an owned
// surface is open - that Escape belongs to the surface (its dismissable layer closes it first).
// Styling lives in app.css under the [data-bz-inspect] / [data-bz-inspect-active] hooks; this
// only stamps attributes.

interface DotNetRef {
    invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

/** portal.ts leaves the element's placeholder comment on the element under this key. */
const PORTAL_ANCHOR = '__bzPortalAnchor';
type Portaled = Element & { [PORTAL_ANCHOR]?: Comment };

class Inspector {
    private readonly observer: MutationObserver;
    private active: HTMLElement | null = null;
    private pending = false;
    private stamped = new Set<HTMLElement>();

    constructor(
        private readonly root: HTMLElement,
        private readonly ref: DotNetRef,
    ) {
        this.stamp();
        // Blazor re-renders replace nodes and surfaces mount on body - re-stamp when either
        // changes (coalesced; rAF alone never ticks in background tabs, so a timeout backs it up).
        this.observer = new MutationObserver(() => this.schedule());
        this.observer.observe(document.body, { childList: true, subtree: true });

        document.addEventListener('pointerover', this.onOver);
        document.addEventListener('keydown', this.onKey);
    }

    /** Whether `el` belongs to the demo: inside the preview, or portaled from a spot that is. */
    private owns(el: Element): boolean {
        let node: Element | null = el;
        while (node) {
            if (node === this.root) return true;
            const anchor = (node as Portaled)[PORTAL_ANCHOR];
            node = anchor ? anchor.parentElement : node.parentElement;
        }
        return false;
    }

    /** Every portaled surface on body (bare or inside a theme frame) the demo owns. */
    private surfaces(): Element[] {
        const out: Element[] = [];
        const scan = (parent: Element) => {
            for (const child of parent.children) {
                if ((child as Portaled)[PORTAL_ANCHOR]) {
                    if (this.owns(child)) out.push(child);
                } else if (child.hasAttribute('data-bz-portal-frame')) {
                    scan(child);
                }
            }
        };
        scan(document.body);
        return out;
    }

    private parts(): HTMLElement[] {
        const out = [...this.root.querySelectorAll<HTMLElement>('[data-slot]')];
        for (const surface of this.surfaces()) {
            if (surface.hasAttribute('data-slot')) out.push(surface as HTMLElement);
            out.push(...surface.querySelectorAll<HTMLElement>('[data-slot]'));
        }
        return out;
    }

    private schedule(): void {
        if (this.pending) return;
        this.pending = true;
        const run = () => {
            if (!this.pending) return;
            this.pending = false;
            this.stamp();
        };
        requestAnimationFrame(run);
        setTimeout(run, 100);
    }

    private stamp(): void {
        const next = new Set(this.parts());
        for (const el of this.stamped) if (!next.has(el)) el.removeAttribute('data-bz-inspect');
        for (const el of next) el.setAttribute('data-bz-inspect', '');
        this.stamped = next;
    }

    private onOver = (event: PointerEvent): void => {
        const el = (event.target as HTMLElement | null)?.closest<HTMLElement>('[data-slot]');
        if (!el || !this.owns(el)) {
            // Pointer left the demo (and its surfaces) - clear the highlight once.
            if (this.active) this.clear();
            return;
        }
        if (el === this.active) return;

        this.active?.removeAttribute('data-bz-inspect-active');
        this.active = el;
        el.setAttribute('data-bz-inspect-active', '');

        // The bz-* markers on the element are its stable styling hooks (utility soup filtered out).
        const hooks = [...el.classList].filter((c) => c.startsWith('bz-') && !c.includes('/'));
        void this.ref.invokeMethodAsync('OnInspectHover', el.getAttribute('data-slot'), el.tagName.toLowerCase(), hooks);
    };

    private onKey = (event: KeyboardEvent): void => {
        if (event.key !== 'Escape' || event.defaultPrevented) return;
        // An open surface takes this Escape (its dismissable layer, in the capture phase before
        // us, has already asked C# to close it). The next one leaves Inspect.
        if (this.surfaces().length > 0) return;
        void this.ref.invokeMethodAsync('OnInspectExit');
    };

    private clear(): void {
        this.active?.removeAttribute('data-bz-inspect-active');
        this.active = null;
        void this.ref.invokeMethodAsync('OnInspectHover', null, null, []);
    }

    dispose = (): void => {
        this.pending = false;
        this.observer.disconnect();
        document.removeEventListener('pointerover', this.onOver);
        document.removeEventListener('keydown', this.onKey);
        this.active?.removeAttribute('data-bz-inspect-active');
        this.active = null;
        for (const el of this.stamped) el.removeAttribute('data-bz-inspect');
        this.stamped.clear();
    };
}

/** Start inspecting a demo preview; returns an instance with dispose(). */
export function inspectStart(root: HTMLElement, ref: DotNetRef): Inspector {
    return new Inspector(root, ref);
}

// Scroll reveal for the landing page: every [data-reveal] descendant of `root` gets
// data-revealed the first time it enters the viewport (CSS in app.css does the fade + rise),
// then stops being watched. Returns a disposable so the page can drop the observer on
// navigation. Without IntersectionObserver everything is revealed at once.
export function revealStart(root: HTMLElement): { dispose(): void } {
    if (!('IntersectionObserver' in window)) return { dispose() { } };
    // Arming is what hides the blocks (app.css gates the hidden state on this attribute), so a
    // page whose script never ran, or a browser without the observer, simply shows everything.
    root.setAttribute('data-reveal-armed', '');
    const targets = root.querySelectorAll<HTMLElement>('[data-reveal]');
    const io = new IntersectionObserver(entries => {
        for (const e of entries) {
            if (!e.isIntersecting) continue;
            (e.target as HTMLElement).setAttribute('data-revealed', '');
            io.unobserve(e.target);
        }
    }, { threshold: 0.15, rootMargin: '0px 0px -8% 0px' });
    targets.forEach(t => io.observe(t));
    return { dispose() { io.disconnect(); } };
}

// ---- Icon browser (the Icons page) --------------------------------------------------------------
// A family file is up to 4 MB of SVG bodies. Parsing that in .NET means the WebAssembly interpreter
// walking every string on the UI thread - tens of seconds for Hugeicons - so the JSON never
// crosses into .NET: JSON.parse here is native and immediate, .NET gets the NAMES only (to filter
// and virtualize), renders each tile as an empty <svg data-icon="Name">, and the fill session
// below writes the body into every such svg as Blazor creates it (a MutationObserver, so scrolling
// the virtualized grid needs no interop at all). data-filled remembers which family a tile holds:
// Blazor keeps a keyed element across a family switch when the name repeats (Heart in both), and
// the stale body must be replaced.

const iconFamilies = new Map<string, Record<string, string>>();

/** Fetches (once) a family file and returns its icon names, in file order. */
export async function iconsLoad(file: string): Promise<string[]> {
    let icons = iconFamilies.get(file);
    if (!icons) {
        const response = await fetch(`icons/${file}`);
        if (!response.ok) throw new Error(`icons/${file}: ${response.status}`);
        icons = ((await response.json()) as { icons: Record<string, string> }).icons;
        iconFamilies.set(file, icons);
    }
    return Object.keys(icons);
}

/** Fills every svg[data-icon] under `root` from `file`, now and as new ones render; dispose() stops. */
export function iconsFillStart(root: HTMLElement, file: string): { dispose(): void } {
    const icons = iconFamilies.get(file) ?? {};
    const fill = () => {
        for (const svg of root.querySelectorAll<SVGElement>('svg[data-icon]')) {
            if (svg.dataset.filled === file) continue;
            const body = icons[svg.dataset.icon ?? ''];
            if (body === undefined) continue;
            svg.innerHTML = body;
            svg.dataset.filled = file;
        }
    };
    fill();
    const observer = new MutationObserver(fill);
    observer.observe(root, { childList: true, subtree: true });
    return { dispose: () => observer.disconnect() };
}
