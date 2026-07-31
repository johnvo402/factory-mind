# 0019 - In-memory access token and refresh cookie

- **Decision:** The Angular client keeps the access token and user session in memory only. It never writes authentication data to `localStorage` or `sessionStorage`.
- **Refresh transport:** Presentation writes the rotating refresh token to an `HttpOnly` cookie with `SameSite=Strict`, path `/api/auth`, a server-controlled expiry, and `Secure` outside Development. Application continues to hash and rotate refresh tokens in PostgreSQL.
- **API response:** Login and refresh JSON expose only the access token and user profile. The raw refresh token is used by Presentation to set the cookie and is never returned to JavaScript.
- **Lifecycle:** On application bootstrap, the client calls refresh to restore an in-memory session. A functional HTTP interceptor adds the bearer token and coordinates one in-flight refresh when concurrent requests receive 401 responses.
- **Logout:** Logout reads and revokes the refresh cookie, then deletes it. It does not require a still-valid access token.
- **Deployment:** The Angular development server proxies `/api` to ASP.NET Core. Production should serve the UI and API from the same site; credentialed CORS remains restricted to the configured frontend origin.
- **Boundary:** The MVP does not add browser storage encryption, cross-tab token broadcasting, OAuth/OIDC, or a separate gateway.
- **Date:** 2026-07-31
