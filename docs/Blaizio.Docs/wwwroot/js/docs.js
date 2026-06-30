// Blaizio.Docs interop module - imported once by the DocsJs service. No window globals;
// everything the app needs from the browser goes through here.

export function copy(text) {
    navigator.clipboard?.writeText(text).catch(() => { });
}

export function getTheme() {
    try { return localStorage.getItem('blaizio-theme') || 'light'; } catch { return 'light'; }
}

export function setTheme(t) {
    try { localStorage.setItem('blaizio-theme', t); } catch { }
    const el = document.documentElement;
    el.classList.toggle('dark', t === 'dark');
    if (t === 'light' || t === 'dark') el.removeAttribute('data-theme');
    else el.setAttribute('data-theme', t);
}

export function getStyle() {
    try { return localStorage.getItem('blaizio-style') || 'ember'; } catch { return 'ember'; }
}

export function setStyle(s) {
    try { localStorage.setItem('blaizio-style', s); } catch { }
    const el = document.documentElement;
    for (const c of [...el.classList]) if (c.startsWith('style-')) el.classList.remove(c);
    el.classList.add('style-' + s);
}

export function getDir() {
    try { return localStorage.getItem('blaizio-dir') || 'ltr'; } catch { return 'ltr'; }
}

export function setDir(d) {
    const dir = d === 'rtl' ? 'rtl' : 'ltr';
    try { localStorage.setItem('blaizio-dir', dir); } catch { }
    document.documentElement.dir = dir;
}
