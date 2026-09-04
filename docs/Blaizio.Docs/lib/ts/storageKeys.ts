// The localStorage keys and injected <style> ids the docs persist theme overrides under. Shared
// by docs.ts (the runtime module: /community "Apply", the /themes composer) and prepaint.ts (the
// classic script that re-injects them before first paint) so the two can never drift apart.

/** A community theme applied from /community: JSON `{ css: string, ... }`. */
export const COMMUNITY_KEY = 'blaizio-docs-community-theme';
export const COMMUNITY_ID = 'bz-community-theme';

/** The /themes composer's edited tokens: raw CSS text. */
export const TOKENS_KEY = 'blaizio-docs-token-overrides';
export const TOKENS_ID = 'bz-token-overrides';
