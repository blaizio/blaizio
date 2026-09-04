// Bundles the docs site's two scripts through esbuild's JS API (same shape as Blaizio.Base's
// lib/build.mjs), invoked by the project's BuildDocsJs target and by the package.json scripts:
//   ts/docs.ts     -> wwwroot/js/docs.js     ESM module, loaded by Blazor via the DocsJs service;
//                     ../_content/* imports stay external (runtime paths into static web assets).
//   ts/prepaint.ts -> wwwroot/js/prepaint.js classic IIFE, a plain script tag before Blazor boots.
// Going through the API instead of `node node_modules/esbuild/bin/esbuild` matters on Linux and
// macOS: esbuild's install step replaces that bin file with the native executable there, so
// handing it to node fails with "SyntaxError: Invalid or unexpected token". Windows keeps the JS
// shim, which is why the old invocation only ever worked on the machines that authored it.
import * as esbuild from 'esbuild';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const tsDir = join(root, 'ts');
const outdir = join(root, '..', 'wwwroot', 'js');
const watch = process.argv.includes('--watch');

/** @type {import('esbuild').BuildOptions} */
const docsOptions = {
  entryPoints: [join(tsDir, 'docs.ts')],
  outfile: join(outdir, 'docs.js'),
  bundle: true,
  format: 'esm',
  external: ['../_content/*'],
  sourcemap: true,
  minify: !watch,
  logLevel: 'info',
};

/** @type {import('esbuild').BuildOptions} */
const prepaintOptions = {
  entryPoints: [join(tsDir, 'prepaint.ts')],
  outfile: join(outdir, 'prepaint.js'),
  bundle: true,
  format: 'iife',
  sourcemap: true,
  minify: !watch,
  logLevel: 'info',
};

if (watch) {
  const ctx = await esbuild.context(docsOptions);
  await ctx.watch();
  console.log('Blaizio.Docs: watching ts/docs.ts ...');
} else {
  await esbuild.build(docsOptions);
  await esbuild.build(prepaintOptions);
  console.log('Blaizio.Docs: bundled docs.js + prepaint.js -> wwwroot/js');
}
