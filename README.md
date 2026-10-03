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
