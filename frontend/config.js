// API base URL — resolved once at page load.
//
// • localhost / 127.0.0.1  →  talk directly to the local .NET backend
// • any other host (Netlify, etc.) →  empty string = same-origin
//   Netlify proxies /api/* to the real backend via netlify.toml [[redirects]]
//
// You never need to edit the HTML files.
// For a new deployment: update the [[redirects]] target in netlify.toml.
window.API_BASE = (
  window.location.hostname === 'localhost' ||
  window.location.hostname === '127.0.0.1'
) ? 'http://localhost:5034' : '';
