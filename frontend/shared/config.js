// API base URL — resolved once at page load.
//
// Same-origin by default, which is correct for both supported ways of running the app:
//   • containers — nginx serves these pages and proxies /api/* to the API container
//   • dotnet run — the API serves these pages itself in Development
//
// Only override when the pages are served from a DIFFERENT origin than the API (e.g. a
// separate static dev server). Two ways, both without editing any HTML:
//   1. set window.API_BASE in a <script> before this file loads, or
//   2. run  localStorage.setItem('apiBase', 'http://localhost:5034')  in the console.
(function () {
  var override = (typeof window.API_BASE === 'string' && window.API_BASE) || '';
  if (!override) {
    try { override = localStorage.getItem('apiBase') || ''; } catch (e) { /* private mode */ }
  }
  window.API_BASE = override.replace(/\/$/, '');
})();
