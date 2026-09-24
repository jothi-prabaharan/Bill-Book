// Deployment settings for this app, read before it starts.
//
// This is the file the build ships, and it deliberately sets nothing: with no
// apiBaseUrl the app calls /api on its own origin, which is what the dev server
// proxies. A deployment overwrites this file with one naming the API origin —
// see deploy/azure — so the same build runs in every environment.
//
// Only non-secret settings belong here. Every browser that loads the app reads
// this file.
window.__BB_CONFIG__ = window.__BB_CONFIG__ || {};
