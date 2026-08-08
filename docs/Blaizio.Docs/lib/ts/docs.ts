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

// Community themes (/community): token overrides shipped as data (a JSON of :root/.dark values),
// not as preset-* classes, so any community-authored palette can apply without a rebuild. The CSS
// is injected as a <style> appended to <head> - same-specificity rules win by source order, so it
// overrides the stylesheet's :root/.dark AND any preset-* class. Persisted as {name, css}; the
// pre-paint counterpart (an inline snippet in index.html, next to boot.js) re-injects it before
// first render so a reload doesn't flash the stock palette.
const COMMUNITY_KEY = 'blaizio-docs-community-theme';
const COMMUNITY_ID = 'bz-community-theme';

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
// style + localStorage + a pre-paint re-inject snippet in index.html.
const TOKENS_KEY = 'blaizio-docs-token-overrides';
const TOKENS_ID = 'bz-token-overrides';

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
// maps the slot to its component type / parameters. All interaction is suppressed (capture-phase)
// while inspecting - the pointer is a probe, not a click. Styling lives in app.css under the
// [data-bz-inspect] / [data-bz-inspect-active] hooks; this only stamps attributes.

interface DotNetRef {
    invokeMethodAsync(method: string, ...args: unknown[]): Promise<unknown>;
}

class Inspector {
    private readonly observer: MutationObserver;
    private active: HTMLElement | null = null;
    private pending = false;

    constructor(
        private readonly root: HTMLElement,
        private readonly ref: DotNetRef,
    ) {
        this.stamp();
        // Blazor re-renders replace nodes - re-stamp when the subtree changes (coalesced; rAF
        // alone never ticks in background tabs, so a timeout backs it up).
        this.observer = new MutationObserver(() => this.schedule());
        this.observer.observe(root, { childList: true, subtree: true });

        this.root.addEventListener('pointerover', this.onOver);
        this.root.addEventListener('pointerleave', this.onLeave);
        for (const type of Inspector.blocked) {
            this.root.addEventListener(type, Inspector.block, { capture: true });
        }
    }

    private static readonly blocked = ['pointerdown', 'pointerup', 'click', 'dblclick', 'keydown'];

    private static block(event: Event): void {
        event.preventDefault();
        event.stopPropagation();
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
        for (const el of this.root.querySelectorAll<HTMLElement>('[data-slot]')) {
            el.setAttribute('data-bz-inspect', '');
        }
    }

    private onOver = (event: PointerEvent): void => {
        const el = (event.target as HTMLElement | null)?.closest<HTMLElement>('[data-slot]');
        if (!el || !this.root.contains(el) || el === this.active) return;

        this.active?.removeAttribute('data-bz-inspect-active');
        this.active = el;
        el.setAttribute('data-bz-inspect-active', '');

        // The bz-* markers on the element are its stable styling hooks (utility soup filtered out).
        const hooks = [...el.classList].filter((c) => c.startsWith('bz-') && !c.includes('/'));
        void this.ref.invokeMethodAsync('OnInspectHover', el.getAttribute('data-slot'), el.tagName.toLowerCase(), hooks);
    };

    private onLeave = (): void => {
        this.active?.removeAttribute('data-bz-inspect-active');
        this.active = null;
        void this.ref.invokeMethodAsync('OnInspectHover', null, null, []);
    };

    dispose = (): void => {
        this.pending = false;
        this.observer.disconnect();
        this.root.removeEventListener('pointerover', this.onOver);
        this.root.removeEventListener('pointerleave', this.onLeave);
        for (const type of Inspector.blocked) {
            this.root.removeEventListener(type, Inspector.block, { capture: true });
        }
        this.active?.removeAttribute('data-bz-inspect-active');
        for (const el of this.root.querySelectorAll<HTMLElement>('[data-bz-inspect]')) {
            el.removeAttribute('data-bz-inspect');
        }
    };
}

/** Start inspecting a demo preview; returns an instance with dispose(). */
export function inspectStart(root: HTMLElement, ref: DotNetRef): Inspector {
    return new Inspector(root, ref);
}

// ---- A11y X-Ray (the Demo shell's accessibility overlay) ----------------------------------------
// Annotates every accessibility-relevant element in a demo preview: an outline plus a floating
// chip showing its ROLE and, when it is a tab stop, its position in the tab order. Hovering an
// annotated element reports the full picture (computed accessible name, aria-* attributes,
// focusability) to C# for the detail panel. Interaction is deliberately NOT blocked - operating
// the demo is the point, so aria-expanded/checked/selected flips re-annotate live (attribute
// mutations re-run the scan). The accessible name here is a spec-shaped approximation
// (labelledby > label > native label > title > text), honestly labelled "computed" in the UI -
// it is not a screen reader simulation.

const IMPLICIT_ROLES: Record<string, string> = {
    button: 'button', select: 'combobox', textarea: 'textbox', img: 'img', nav: 'navigation',
    table: 'table', ul: 'list', ol: 'list', li: 'listitem', dialog: 'dialog', mark: 'mark',
};

function implicitRole(el: HTMLElement): string | null {
    const tag = el.tagName.toLowerCase();
    if (tag === 'a') return el.hasAttribute('href') ? 'link' : null;
    if (tag === 'input') {
        const type = (el as HTMLInputElement).type;
        return type === 'checkbox' ? 'checkbox'
            : type === 'radio' ? 'radio'
            : type === 'range' ? 'slider'
            : type === 'button' || type === 'submit' ? 'button'
            : 'textbox';
    }
    return IMPLICIT_ROLES[tag] ?? null;
}

function roleOf(el: HTMLElement): string | null {
    return el.getAttribute('role') ?? implicitRole(el);
}

function isFocusable(el: HTMLElement): boolean {
    if (el.hasAttribute('disabled') || el.getAttribute('aria-disabled') === 'true') return false;
    const tabindex = el.getAttribute('tabindex');
    if (tabindex !== null) return parseInt(tabindex, 10) >= 0;
    const tag = el.tagName.toLowerCase();
    return tag === 'button' || tag === 'select' || tag === 'textarea'
        || (tag === 'a' && el.hasAttribute('href'))
        || (tag === 'input' && (el as HTMLInputElement).type !== 'hidden');
}

/** Spec-shaped accessible-name approximation: labelledby > aria-label > native label > title > text. */
function accName(el: HTMLElement): string {
    const labelledby = el.getAttribute('aria-labelledby');
    if (labelledby) {
        const text = labelledby.split(/\s+/)
            .map((id) => document.getElementById(id)?.textContent?.trim() ?? '')
            .filter(Boolean).join(' ');
        if (text) return text;
    }
    const label = el.getAttribute('aria-label');
    if (label) return label;
    const id = el.getAttribute('id');
    if (id) {
        const native = document.querySelector(`label[for="${CSS.escape(id)}"]`)?.textContent?.trim();
        if (native) return native;
    }
    const title = el.getAttribute('title');
    if (title) return title;
    return (el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 120);
}

class A11yXray {
    private readonly overlay: HTMLElement;
    private readonly mutationObserver: MutationObserver;
    private readonly resizeObserver: ResizeObserver;
    private annotated: HTMLElement[] = [];
    private active: HTMLElement | null = null;
    private pending = false;

    constructor(
        private readonly root: HTMLElement,
        private readonly ref: DotNetRef,
    ) {
        if (getComputedStyle(root).position === 'static') root.style.position = 'relative';
        this.overlay = document.createElement('div');
        this.overlay.className = 'bz-a11y-overlay';
        root.appendChild(this.overlay);

        // Ignore our own writes (the data-bz-a11y stamps and overlay chips), or every scan would
        // schedule the next one forever.
        this.mutationObserver = new MutationObserver((records) => {
            const foreign = records.some((r) =>
                !(r.type === 'attributes' && r.attributeName === 'data-bz-a11y')
                && r.target !== this.overlay && !this.overlay.contains(r.target));
            if (foreign) this.schedule();
        });
        this.mutationObserver.observe(root, { childList: true, subtree: true, attributes: true });
        this.resizeObserver = new ResizeObserver(() => this.schedule());
        this.resizeObserver.observe(root);

        this.root.addEventListener('pointerover', this.onOver);
        this.root.addEventListener('pointerleave', this.onLeave);
        this.scan();
    }

    private schedule(): void {
        if (this.pending) return;
        this.pending = true;
        const run = () => {
            if (!this.pending) return;
            this.pending = false;
            this.scan();
        };
        requestAnimationFrame(run);
        setTimeout(run, 100);
    }

    private targets(): HTMLElement[] {
        const found: HTMLElement[] = [];
        for (const el of this.root.querySelectorAll<HTMLElement>('*')) {
            if (this.overlay.contains(el)) continue;
            const hasAria = el.getAttributeNames().some((n) => n.startsWith('aria-'));
            if (roleOf(el) !== null || hasAria || isFocusable(el)) found.push(el);
        }
        return found;
    }

    /** DOM order = tab order here (the library never uses positive tabindex). */
    private tabStop(el: HTMLElement, stops: HTMLElement[]): number {
        const index = stops.indexOf(el);
        return index < 0 ? -1 : index + 1;
    }

    private scan(): void {
        for (const el of this.annotated) el.removeAttribute('data-bz-a11y');
        this.overlay.replaceChildren();

        this.annotated = this.targets();
        const stops = this.annotated.filter(isFocusable);
        const rootRect = this.root.getBoundingClientRect();

        for (const el of this.annotated) {
            el.setAttribute('data-bz-a11y', '');
            const role = roleOf(el);
            const stop = this.tabStop(el, stops);
            if (role === null && stop < 0) continue; // aria-only carriers keep the outline, no chip

            const rect = el.getBoundingClientRect();
            const chip = document.createElement('span');
            chip.className = 'bz-a11y-chip';
            chip.textContent = (role ?? '') + (stop > 0 ? `${role ? ' ' : ''}⇥${stop}` : '');
            chip.style.left = `${Math.max(0, rect.left - rootRect.left)}px`;
            chip.style.top = `${Math.max(0, rect.top - rootRect.top - 14)}px`;
            this.overlay.appendChild(chip);
        }
    }

    private onOver = (event: PointerEvent): void => {
        const el = (event.target as HTMLElement | null)?.closest<HTMLElement>('[data-bz-a11y]');
        if (!el || !this.root.contains(el) || el === this.active) return;
        this.active = el;

        const aria = el.getAttributeNames()
            .filter((n) => n.startsWith('aria-'))
            .map((n) => `${n}="${el.getAttribute(n)}"`);
        const stops = this.annotated.filter(isFocusable);
        void this.ref.invokeMethodAsync(
            'OnA11yHover', roleOf(el), accName(el), this.tabStop(el, stops), aria, el.tagName.toLowerCase());
    };

    private onLeave = (): void => {
        this.active = null;
        void this.ref.invokeMethodAsync('OnA11yHover', null, null, -1, [], null);
    };

    dispose = (): void => {
        this.pending = false;
        this.mutationObserver.disconnect();
        this.resizeObserver.disconnect();
        this.root.removeEventListener('pointerover', this.onOver);
        this.root.removeEventListener('pointerleave', this.onLeave);
        for (const el of this.annotated) el.removeAttribute('data-bz-a11y');
        this.overlay.remove();
    };
}

/** Start the a11y overlay on a demo preview; returns an instance with dispose(). */
export function a11yStart(root: HTMLElement, ref: DotNetRef): A11yXray {
    return new A11yXray(root, ref);
}
