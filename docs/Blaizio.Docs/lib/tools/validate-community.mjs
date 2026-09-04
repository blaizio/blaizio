// Validates the community directory files (wwwroot/community/registries.json + themes.json):
// the structural rules a listing pull request must pass. Run locally with `pnpm validate:community`;
// the community-directory workflow runs the same script on every PR touching those files, so a
// submission that passes here passes review's structural gate. Reachability is NOT checked here -
// that stays an advisory probe in CI, where a listed host being down warns instead of blocking.
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

// An explicit directory argument overrides the default - lets tests point at fixtures.
const root = process.argv[2] ?? join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'wwwroot', 'community');
const errors = [];

function load(file) {
    try {
        const parsed = JSON.parse(readFileSync(join(root, file), 'utf8'));
        if (!Array.isArray(parsed)) {
            errors.push(`${file}: the top level must be an array`);
            return [];
        }
        return parsed;
    } catch (e) {
        errors.push(`${file}: ${e.message}`);
        return [];
    }
}

function uniqueNames(file, entries) {
    const seen = new Map();
    for (const entry of entries) {
        const key = String(entry.name ?? '').toLowerCase();
        if (seen.has(key)) errors.push(`${file}: duplicate name '${entry.name}'`);
        seen.set(key, true);
    }
}

const nonEmpty = v => typeof v === 'string' && v.trim().length > 0;
const httpsUrl = v => nonEmpty(v) && v.startsWith('https://');

// --- registries.json: pointers to hosted registries ---
const registries = load('registries.json');
registries.forEach((r, i) => {
    const at = `registries.json entry ${i} ('${r.name ?? '?'}')`;
    if (!nonEmpty(r.name) || !/^@[a-z0-9][a-z0-9-]*$/.test(r.name))
        errors.push(`${at}: name must be a lowercase @namespace (letters, digits, dashes)`);
    if (!httpsUrl(r.homepage)) errors.push(`${at}: homepage must be an https:// URL`);
    if (!httpsUrl(r.url)) errors.push(`${at}: url must be an https:// URL (the built registry base)`);
    if (!nonEmpty(r.description)) errors.push(`${at}: description must be one non-empty line`);
});
uniqueNames('registries.json', registries);

// --- themes.json: full theme data, applied client-side on the community page ---
const themes = load('themes.json');
themes.forEach((t, i) => {
    const at = `themes.json entry ${i} ('${t.name ?? '?'}')`;
    if (!nonEmpty(t.name) || !/^[a-z0-9][a-z0-9-]*$/.test(t.name))
        errors.push(`${at}: name must be a lowercase slug (letters, digits, dashes)`);
    if (!nonEmpty(t.title)) errors.push(`${at}: title is required`);
    if (!nonEmpty(t.author)) errors.push(`${at}: author (your handle) is required`);
    if (!nonEmpty(t.description)) errors.push(`${at}: description must be one non-empty line`);
    const maps = ['light', 'dark'].filter(k => t[k] !== undefined);
    if (maps.length === 0) errors.push(`${at}: at least one of light/dark is required`);
    for (const k of maps) {
        if (typeof t[k] !== 'object' || t[k] === null || Array.isArray(t[k])) {
            errors.push(`${at}: ${k} must be an object of token values`);
            continue;
        }
        for (const [token, value] of Object.entries(t[k]))
            if (!nonEmpty(value)) errors.push(`${at}: ${k}.${token} must be a non-empty string`);
    }
});
uniqueNames('themes.json', themes);

if (errors.length > 0) {
    for (const e of errors) console.error(`error: ${e}`);
    process.exit(1);
}
console.log(`community directory ok: ${registries.length} registr${registries.length === 1 ? 'y' : 'ies'}, ${themes.length} theme${themes.length === 1 ? '' : 's'}`);
