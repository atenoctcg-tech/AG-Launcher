# AG Launcher

Atenoct Games Launcher — a modern Windows game launcher for Castle Survival and future Atenoct Games titles.

## Design
Modern dark interface: black, charcoal, graphite, soft grey and white. No beige/ornamental UI.

## Features
- Multi-game catalog
- Install / Update / Play
- GitHub-hosted manifests
- SHA-256 package verification
- Mandatory launcher self-update
- NEWS feed
- Discord / Telegram / YouTube links
- Hidden GitHub-verified admin panel (Ctrl + Shift + A)
- Future-ready paid-game entitlement field

## Build
```powershell
.\scripts\Build-Windows.ps1
```

Do not commit private API keys, service-account credentials, GitHub PATs, signing secrets, or paid-entitlement secrets.

## v0.4.0 Studio design

Website: `docs/` (GitHub Pages). Launcher: native .NET 8 WPF, Windows x64. Both read `launcher-manifest.json`, including news, games, social links, theme, Workshop and account endpoint.

- Original supplied gold logo, app icon and Castle Survival artwork; preserved aspect ratio and high-quality logo scaling.
- Dark navy/charcoal panels, white text, gentle hover zoom and page entrance motion. Website respects reduced-motion preferences; Windows follows system animation settings. Studio admin can disable motion globally.
- HOME, MY LIBRARY, WORKSHOP; image posts; editable links; GitHub release ZIP detection, in-app update notifications every two minutes while running, INSTALL / UPDATE / PLAY. Notifications appear when the launcher is open; there is no background service when it is closed.
- Visual website administrator: `docs/admin.html`. Native admin: Ctrl+Shift+A, or Studio administrator setup on sign-in. A GitHub token with repository write permission is required to publish. Tokens are not written into public content.
- Add future games using either a manifest URL or owner/repository, Windows ZIP regex and executable path. Do not upload Source ZIPs matching the game build pattern. Paid games remain unavailable until a future payment/ownership integration.
- Mandatory verified email login is implemented, but **account hosting and email delivery must be configured before this Windows release can sign in**. See `server/README.md`. The old release is not forced to update while this setup is pending.

Build: `powershell -ExecutionPolicy Bypass -File scripts/Build-Windows.ps1`. Windows ZIP contains `AGLauncher.exe`, updater and all required runtime files; extract the entire ZIP to one directory before running. Do not copy only the EXE. Source ZIP contains website, native launcher, account server and build workflow.
