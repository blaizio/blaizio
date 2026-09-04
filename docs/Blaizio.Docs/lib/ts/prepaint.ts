// Pre-paint re-inject of the persisted theme overrides - the counterpart of docs.ts's
// setCommunityTheme (/community "Apply") and setTokenOverrides (/themes composer). Those write a
// <style> into <head> at runtime; on the next load this puts the same <style> back BEFORE first
// render, so a reload never flashes the stock palette. It exists for the same reason as
// Blaizio.Base's boot.js: services (and the docs.js module behind IDocsJs) only come up after
// Blazor boots, far too late.
//
// Bundled as a classic script (esbuild --format=iife -> wwwroot/js/prepaint.js) and loaded from a
// plain <script> tag in index.html right after boot.js. No exports, no globals: it runs once at
// head-parse time and is done.

import { COMMUNITY_ID, COMMUNITY_KEY, TOKENS_ID, TOKENS_KEY } from './storageKeys';

function inject(id: string, css: string): void {
  const style = document.createElement('style');
  style.id = id;
  style.textContent = css;
  document.head.appendChild(style);
}

try {
  const theme = JSON.parse(localStorage.getItem(COMMUNITY_KEY) || 'null') as { css?: string } | null;
  if (theme?.css) inject(COMMUNITY_ID, theme.css);
} catch {
  // Storage blocked or the entry malformed: paint the stock palette.
}

try {
  const overrides = localStorage.getItem(TOKENS_KEY);
  if (overrides) inject(TOKENS_ID, overrides);
} catch {
  // Same.
}
