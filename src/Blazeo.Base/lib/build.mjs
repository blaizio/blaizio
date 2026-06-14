// Bundles every entry in ts/ to an ESM module in wwwroot/dist, loaded at runtime via
//   import('./_content/Blazeo.Base/dist/<name>.js')
// esbuild transpiles + bundles (incl. node_modules like @floating-ui); shared deps are
// split into chunks so a heavy dependency isn't duplicated across entry modules.
import * as esbuild from 'esbuild';
import { readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const tsDir = join(root, 'ts');
// This script lives in lib/; the bundle output goes to the project's wwwroot/dist (served at
// _content/Blazeo.Base/dist), one level up from here.
const outdir = join(root, '..', 'wwwroot', 'dist');
const watch = process.argv.includes('--watch');

const entryPoints = readdirSync(tsDir)
  .filter((f) => f.endsWith('.ts') && !f.endsWith('.d.ts'))
  .map((f) => join(tsDir, f));

/** @type {import('esbuild').BuildOptions} */
const options = {
  entryPoints,
  outdir,
  bundle: true,
  format: 'esm',
  splitting: true,
  sourcemap: true,
  minify: !watch,
  target: ['es2022'],
  logLevel: 'info',
};

if (watch) {
  const ctx = await esbuild.context(options);
  await ctx.watch();
  console.log('Blazeo.Base: watching ts/ ...');
} else {
  await esbuild.build(options);
  console.log(`Blazeo.Base: bundled ${entryPoints.length} module(s) -> wwwroot/dist`);
}
