// In development, always fetch from the network and do not enable offline support.
// Caching would make development harder (changes would not show on the first reload).
self.addEventListener('fetch', () => { });
