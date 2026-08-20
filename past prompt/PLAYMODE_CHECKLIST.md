# Play-Mode Verification Checklist
# Run this after Opus 5 builds the 3D Scavenge scene.
# Open in Unity Hub → Assets/Scenes/_Bootstrap.unity → Play

## 1. COMPILE CHECK (before opening Unity)
```bash
cd ~/projects/OblastZero && python3 tools/verify_steam_layer.py
# Must show: 39/39 checks passed, ALL GREEN
```
If not green → fix compile errors first. Unity won't load the scene with errors.

## 2. BOOT FLOW
- [ ] Open _Bootstrap.unity
- [ ] Press Play
- [ ] Console shows: "[Bootstrap] OBLAST ZERO — Bootstrap awake"
- [ ] Console shows: "[SteamManager]" init message (or offline warning if Steam not running)
- [ ] Console shows: "[GameManager]" initialization
- [ ] State transitions to MainMenu
- [ ] MainMenu UI appears on screen

## 3. MAIN MENU → RUN SETUP
- [ ] MainMenu shows: New Run, Continue (if save exists), Quit
- [ ] Click New Run
- [ ] RunSetup screen appears
- [ ] Select scavenge site
- [ ] Select crew members
- [ ] Click Begin Run / Start

## 4. SCAVENGE PHASE (3D — the new scene)
- [ ] Scene loads (no black screen, no errors)
- [ ] First-person camera works (mouse look)
- [ ] WASD movement works
- [ ] Shift to sprint
- [ ] E key picks up items when looking at them
- [ ] ScavengeHUD shows: countdown timer (starts at 60)
- [ ] Timer ticks down each second
- [ ] Timer color changes: white → orange (15s) → red (5s)
- [ ] Pickup prompt appears when looking at pickup ("[E] Take" or "[E] Rescue")
- [ ] Grabbed items list updates in HUD
- [ ] Can see level geometry (walls, crates, silos)
- [ ] Fog/atmosphere visible
- [ ] Bunker entrance visible and marked
- [ ] Walk to bunker entrance → triggers ReachBunkerEvent
- [ ] Phase transitions to TransitionCutscene

## 5. TRANSITION → BUNKER PHASE (2D)
- [ ] Cutscene plays (or transition happens)
- [ ] Bunker scene loads
- [ ] BunkerHUD shows: Day 1, crew count, rations, reputation
- [ ] EventModalUI appears if an event is pending

## 6. BUNKER DAY LOOP
- [ ] Click End Day
- [ ] Day advances (Day 2, 3, etc.)
- [ ] Crew consumes rations
- [ ] Events appear with choices
- [ ] Click a choice → outcome panel shows
- [ ] Crew stats change (health/sanity/radiation)
- [ ] Let crew die (starve them) → CrewDiedEvent fires

## 7. RUN END → RUN FAILED STATE
- [ ] When all crew dead (or conditions met) → RunFailedState activates
- [ ] Summary screen appears with run stats
- [ ] "Return to Main Menu" button works
- [ ] Returns to MainMenu without errors

## 8. CONSOLE ERRORS
- [ ] Play through entire flow
- [ ] Check console: ZERO red errors
- [ ] Yellow warnings OK (common in Unity)
- [ ] Save any errors you see with screenshots

## 9. STEAM (optional — only if Steam running)
- [ ] SteamManager initializes without error
- [ ] No SteamAchievementsService crashes
- [ ] If you trigger an achievement condition, it logs

## IF SOMETHING BREAKS
1. Note the exact console error
2. Screenshot it
3. Tell Hermes what happened — paste the error
4. We'll fix it or adjust the prompt for Opus 5
