// Blazeo.Docs interop module - imported once by the DocsJs service. No window globals;
// everything the app needs from the browser goes through here.

export function copy(text) {
    navigator.clipboard?.writeText(text).catch(() => { });
}

export function getTheme() {
    try { return localStorage.getItem('blazeo-theme') || 'light'; } catch { return 'light'; }
}

export function setTheme(t) {
    try { localStorage.setItem('blazeo-theme', t); } catch { }
    const el = document.documentElement;
    el.classList.toggle('dark', t === 'dark');
    if (t === 'light' || t === 'dark') el.removeAttribute('data-theme');
    else el.setAttribute('data-theme', t);
}

export function getStyle() {
    try { return localStorage.getItem('blazeo-style') || 'ember'; } catch { return 'ember'; }
}

export function setStyle(s) {
    try { localStorage.setItem('blazeo-style', s); } catch { }
    const el = document.documentElement;
    for (const c of [...el.classList]) if (c.startsWith('style-')) el.classList.remove(c);
    el.classList.add('style-' + s);
}
