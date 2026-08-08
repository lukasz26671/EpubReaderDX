// Caution! Be sure you understand the caveats before publishing an application with
// offline support. See https://aka.ms/blazor-offline-considerations

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html$/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/, /\.svg$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

// Resolve against the service worker URL so GitHub Pages project sites (/repo/) work.
const baseUrl = new URL('./', self.location);
const manifestUrlList = self.assetsManifest.assets
    .map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');

    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => {
            const url = new URL(asset.url, baseUrl).href;
            return new Request(url, { integrity: asset.hash, cache: 'no-cache' });
        });

    // Cache in batches — addAll fails entirely if one request fails.
    const cache = await caches.open(cacheName);
    const batchSize = 40;
    for (let i = 0; i < assetsRequests.length; i += batchSize) {
        const batch = assetsRequests.slice(i, i + batchSize);
        await Promise.all(batch.map(async request => {
            try {
                await cache.add(request);
            } catch (err) {
                console.warn('Service worker: failed to cache', request.url, err);
            }
        }));
    }
}

async function onActivate(event) {
    console.info('Service worker: Activate');
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);

        const request = shouldServeIndexHtml
            ? new Request(new URL('index.html', baseUrl))
            : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }

    if (cachedResponse && cachedResponse.redirected) {
        const clonedResponse = cachedResponse.clone();
        cachedResponse = new Response(clonedResponse.body, {
            headers: clonedResponse.headers,
            status: cachedResponse.status,
            statusText: cachedResponse.statusText
        });
    }

    return cachedResponse || fetch(event.request);
}
