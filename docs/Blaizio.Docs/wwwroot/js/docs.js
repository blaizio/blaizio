// Blaizio.Docs interop module - imported once by the DocsJs service. No window globals;
// everything the app needs from the browser goes through here.

// Theme / style / direction switching comes from Blaizio.Base's theme module (persisted to
// localStorage; the pre-paint counterpart is its dist/boot.js, loaded in index.html <head>).
export { getTheme, setTheme, getStyle, setStyle, getDir, setDir } from '../_content/Blaizio.Base/dist/theme.js';

export function copy(text) {
    navigator.clipboard?.writeText(text).catch(() => { });
}

// Activity scrollbars + edge fades: on any [data-scroll-activity] element, reveal the scrollbar
// only WHILE it is being scrolled (fading out ~900ms after), and record whether the content is
// currently at the top / bottom so the `scroll-fade-y` mask only fades an edge that actually has
// content beyond it (so the first row stays crisp when scrolled to the top). Uses a capturing
// listener (installed once, on first import) so it also covers elements Blazor mounts later
// (scroll doesn't bubble).

// Mark which edges are scrolled-away (1px slack absorbs sub-pixel rounding).
function fadeEdges(el) {
    el.dataset.atTop = String(el.scrollTop <= 1);
    el.dataset.atBottom = String(el.scrollTop + el.clientHeight >= el.scrollHeight - 1);
}

const scrollTimers = new WeakMap();
document.addEventListener('scroll', (e) => {
    const el = e.target;
    if (!el || el.nodeType !== 1 || !el.matches || !el.matches('[data-scroll-activity]')) return;
    el.classList.add('is-scrolling');
    fadeEdges(el);
    clearTimeout(scrollTimers.get(el));
    scrollTimers.set(el, setTimeout(() => el.classList.remove('is-scrolling'), 900));
}, true);

// Re-measure every activity element (called by Blazor after the nav list (re)renders).
export function scrollFadeRefresh() {
    document.querySelectorAll('[data-scroll-activity]').forEach(fadeEdges);
}

// Sliding active-page indicator for the sidebar nav. Each [data-nav-list] holds one
// [data-nav-indicator]; we move it to the active row (a[aria-current=page]) so it animates from
// the old row to the new one via its CSS transition. NavMenu calls navPosition() after each
// render (i.e. after every navigation / tab switch); a ResizeObserver keeps it aligned on
// layout changes.
function placeIndicator(list) {
    const ind = list.querySelector('[data-nav-indicator]');
    if (!ind) return;
    const active = list.querySelector('a[aria-current="page"]');
    if (!active) { ind.dataset.show = 'false'; return; }
    const lr = list.getBoundingClientRect(), ar = active.getBoundingClientRect();
    ind.style.setProperty('--nav-ind-y', (ar.top - lr.top) + 'px');
    ind.style.setProperty('--nav-ind-h', ar.height + 'px');
    ind.dataset.show = 'true';
}

let navResizeObserver;
export function navPosition() {
    const lists = document.querySelectorAll('[data-nav-list]');
    if (!navResizeObserver) { navResizeObserver = new ResizeObserver(() => lists.forEach(placeIndicator)); }
    lists.forEach(l => { placeIndicator(l); try { navResizeObserver.observe(l); } catch { } });
}

// Scroll the active row into view inside the sidebar's own scroller - used once on load, so a
// deep link (/components/tooltip) opens with its nav row visible.
export function navReveal() {
    const active = document.querySelector('[data-scroll-activity] a[aria-current="page"]');
    if (!active) return;
    const scroller = active.closest('[data-scroll-activity]');
    if (!scroller) return;
    const sr = scroller.getBoundingClientRect(), ar = active.getBoundingClientRect();
    if (ar.top < sr.top || ar.bottom > sr.bottom) {
        scroller.scrollTop += ar.top - sr.top - (sr.height - ar.height) / 2;
    }
}
