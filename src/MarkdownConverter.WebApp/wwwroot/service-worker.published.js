// Caution! Ensure this file is NOT in development; it caches assets aggressively.
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => {
    if (event.request.method === 'GET') {
        event.respondWith(onFetch(event));
    }
});

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.woff2$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];
const scopeUrl = new URL(self.registration.scope);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, scopeUrl).href);

async function onInstall(event) {
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(new URL(asset.url, scopeUrl), { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
}

async function onActivate(event) {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    const isNavigation = event.request.mode === 'navigate';
    const shouldServeIndexHtml = isNavigation
        && !manifestUrlList.some(url => url === event.request.url);
    const request = shouldServeIndexHtml
        ? new Request(new URL('index.html', scopeUrl))
        : event.request;

    try {
        const cache = await caches.open(cacheName);
        const cachedResponse = await cache.match(request);
        if (cachedResponse) return cachedResponse;
    } catch {
        // Cache Storage can be unavailable or cleared independently of the worker.
    }

    try {
        return await fetch(event.request);
    } catch {
        if (isNavigation) {
            return new Response(
                '<!doctype html><meta charset="utf-8"><title>MD Converter unavailable</title>'
                + '<p>MD Converter is temporarily unavailable. Reconnect and reload.</p>',
                {
                    status: 503,
                    statusText: 'Service Unavailable',
                    headers: {
                        'Content-Type': 'text/html; charset=utf-8',
                        'Cache-Control': 'no-store'
                    }
                });
        }

        return new Response(null, {
            status: 503,
            statusText: 'Service Unavailable'
        });
    }
}
