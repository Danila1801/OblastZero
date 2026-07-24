# Steamworks Integration (Facepunch.Steamworks)

## Install (one-time)

1. **Download Facepunch.Steamworks**: https://github.com/Facepunch/Facepunch.Steamworks/releases
   - Get `Facepunch.Steamworks.Posix.dll` + `libsteam_api.so` (Linux) OR
   - `Facepunch.Steamworks.Win64.dll` + `steam_api64.dll` (Windows)
2. Place in `Assets/Plugins/Facepunch.Steamworks/`
3. Place native libs (`steam_api64.dll`, `libsteam_api.so`) in same folder — Unity auto-detects
4. Add `STEAMWORKS` to Scripting Define Symbols:
   - Edit → Project Settings → Player → Other Settings → Scripting Define Symbols
   - Append `;STEAMWORKS` to each platform (PC/Mac/Linux)
5. Get your **Steam App ID** from https://partner.steamgames.com (or use `480` for testing — Spacewar demo)
6. Create a `steam_appid.txt` in project root with just the App ID (e.g. `480`) for editor playmode

## What's implemented

- `SteamManager` (singleton, boots before `GameManager`, pumps callbacks every frame)
- `SteamAchievementsService` (unlock by string key, batch queries)
- `SteamStatsService` (set int stats, auto-indicate/store)
- `SteamCloudSave` (backup `SaveSystem` JSON to cloud, restore on new machine)
- `SteamConfig` ScriptableObject (App ID + achievement IDs + stat IDs, single source of truth)
- All compile-guarded behind `#if STEAMWORKS` so project still builds without the SDK

## Event hooks

- `RunStarted` → stat `runs_started++`
- `RunEnded` (victory or wipe) → stat `runs_ended++`, check wipe/victory achievements
- `DayAdvancedEvent` → stat `days_survived_total`, check "survive 10/30/60 days"
- `FactionReputationChangedEvent` → check "reach +60 rep with any faction"
- `CrewDiedEvent` → stat `crew_deaths_total`

## Manual setup required (Steamworks portal)

Go to https://partner.steamgames.com → your app:
1. **Steamworks Admin → Stats & Achievements** — create each achievement/stat listed in `SteamConfig`
2. **Steamworks Admin → Cloud** — enable cloud storage, set byte quota (64MB is fine)
3. **Install/Build tab** — upload depot via SteamCMD (later)
4. **App Admin → Edit Store Page** — capsule art, trailer, description

## Run without Steam

Remove `STEAMWORKS` from Define Symbols → all integration classes compile to noops,
`GameManager.Boot()` skips `SteamManager.Initialize()`, game runs fully offline.
This is the recommended dev workflow until you're ready for Steam submission.
