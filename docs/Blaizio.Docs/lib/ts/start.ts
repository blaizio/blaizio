// Starts Blazor by hand, with a boot resource loader that recovers from a bad HTTP cache entry.
//
// The .NET runtime fetches every boot file that carries an integrity hash (the assemblies, the
// native wasm, the ICU data, the symbols) with `cache: 'force-cache'`: the names are content
// fingerprints, so a stored copy never needs revalidating. The same mode also reuses a stored
// ERROR forever. A static host answers a file it does not have yet (a deploy still rolling out)
// with its 404 page and no Cache-Control, the browser keeps that 404, and from then on
// `force-cache` hands it back without a request, the integrity check rejects it and the fetch
// fails with "Failed to fetch". Blazor turns the runtime's own download retry off, so that
// browser sat on the splash on every visit until its site data was cleared.
//
// The loader below makes the same first request the runtime would. When that fails it asks once
// more with `cache: 'reload'`, which goes to the network and overwrites the stored entry, so the
// visit boots and the next one is served from the cache again. A file that is really missing or
// really corrupt fails the second request too, and Blazor reports it as before.
//
// Bundled as an ES module (lib/build.mjs -> wwwroot/js/start.js) and loaded from index.html right
// after blazor.webassembly.js, whose script tag carries autostart="false". No exports, no globals.

type BootResourceLoader = (
  type: string,
  name: string,
  defaultUri: string,
  integrity: string,
) => string | Promise<Response> | null | undefined;

declare const Blazor: {
  start(options?: { loadBootResource?: BootResourceLoader }): Promise<void>;
};

// The types the runtime fetches with `force-cache` plus an integrity hash. Everything else keeps
// the framework's own loading: 'dotnetjs' is imported, not fetched (a custom loader may only
// answer it with a URI), and 'manifest' and 'configuration' are fetched with `no-cache`, which
// revalidates on its own.
const RETRIED = new Set(['assembly', 'pdb', 'dotnetwasm', 'globalization']);

async function fetchBootFile(name: string, uri: string, integrity: string): Promise<Response> {
  try {
    // The runtime's own request, option for option.
    const response = await fetch(uri, { cache: 'force-cache', integrity });
    if (response.ok) return response;
  } catch {
    // An integrity mismatch or a network error: both reject. Ask again below.
  }
  console.warn(`Boot file ${name} failed to load, fetching it again past the HTTP cache.`);
  return fetch(uri, { cache: 'reload', integrity });
}

void Blazor.start({
  loadBootResource: (type, name, defaultUri, integrity) =>
    integrity && RETRIED.has(type) ? fetchBootFile(name, defaultUri, integrity) : null,
});
