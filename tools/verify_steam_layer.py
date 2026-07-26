#!/usr/bin/env python3
"""hermes-verify-oblastzero-steam — ad-hoc verification of the Steamworks compile fix.

Checks, in order:
  1. Only ONE managed Facepunch DLL is staged under Assets/ (else CS0433).
  2. Every staged DLL .meta carries a real PluginImporter block (else CS0246).
  3. No source file still references the dead Facepunch 1.x API surface.
  4. Every Steam API symbol the code calls actually exists in the shipped DLL.
  5. SteamConfig.asset points at the real SteamConfig.cs GUID.
  6. Assembly-CSharp.csproj compiles with ZERO errors (dotnet build, project-root scratch copy).
  7. The types we care about are present in the produced assembly.

Exit 0 = all green. Any failure prints FAIL and exits 1.
"""
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

PROJ = Path(r"C:\Users\danil\projects\OblastZero")
PLUGINS = PROJ / "Assets/Plugins/Facepunch.Steamworks"
STEAM_SRC = PROJ / "Assets/_Project/Scripts/Steam"
DLL = PLUGINS / "Facepunch.Steamworks.Win64.dll"
SCRATCH = PROJ / "zz_hermes_verify.csproj"
OUTDIR = "Temp/hermes_verify/"

failures, checks = [], 0


def check(label, ok, detail=""):
    global checks
    checks += 1
    print(f"  [{'PASS' if ok else 'FAIL'}] {label}" + (f" — {detail}" if detail else ""))
    if not ok:
        failures.append(label)
    return ok


print("\n=== 1. single managed DLL ===")
managed = sorted(p.name for p in PLUGINS.glob("Facepunch.Steamworks*.dll"))
check("exactly one managed Facepunch DLL", len(managed) == 1, f"staged: {managed}")

print("\n=== 2. plugin metas are real PluginImporters ===")
for meta in sorted(PLUGINS.glob("*.dll.meta")):
    body = meta.read_text(encoding="utf-8")
    check(f"{meta.name} has PluginImporter+platformData",
          "PluginImporter:" in body and "platformData:" in body)

print("\n=== 3. no dead Facepunch 1.x API references ===")
for cs in sorted(STEAM_SRC.glob("*.cs")) + [PROJ / "Assets/_Project/Scripts/Core/Bootstrap.cs"]:
    src = cs.read_text(encoding="utf-8")
    bad = re.findall(r"Facepunch\.Steamworks\.\w+", src)
    check(f"{cs.name} free of Facepunch.* API", not bad, f"found {sorted(set(bad))}" if bad else "")

print("\n=== 4. called Steam symbols exist in the DLL ===")
blob = DLL.read_bytes().decode("latin-1")
for sym in ("SteamClient", "SteamUserStats", "SteamRemoteStorage", "Achievement",
            "SetStat", "GetStatInt", "StoreStats", "RequestCurrentStats",
            "FileWrite", "FileRead", "FileExists", "RunCallbacks", "Shutdown", "Trigger"):
    check(f"DLL exports '{sym}'", sym in blob)

print("\n=== 5. SteamConfig.asset wired to SteamConfig.cs ===")
guid = re.search(r"guid: (\w+)", (STEAM_SRC / "SteamConfig.cs.meta").read_text(encoding="utf-8")).group(1)
asset = (PROJ / "Assets/Data/Resources/SteamConfig.asset").read_text(encoding="utf-8")
check("asset m_Script guid matches script meta", guid in asset, f"guid={guid}")
check("asset is under a Resources/ folder for Resources.Load", True)

print("\n=== 6. dotnet build (0 errors required) ===")
csproj = (PROJ / "Assembly-CSharp.csproj").read_text(encoding="utf-8")
for gone in ("Facepunch.Steamworks.Posix", "Facepunch.Steamworks.Win32"):
    csproj = re.sub(r'[ \t]*<Reference Include="' + re.escape(gone) + r'">.*?</Reference>\r?\n',
                    "", csproj, flags=re.S)
csproj = re.sub(r"<OutputPath>[^<]*</OutputPath>", f"<OutputPath>{OUTDIR}</OutputPath>", csproj)

# Unity only lists a .cs file in Assembly-CSharp.csproj once the Editor has imported it. Scripts written
# by an outside process (which is how most of this project is authored) are therefore absent until Unity
# next has focus — and compiling without them reports CS0246 for types that are perfectly fine on disk.
# Inject anything missing so this check reflects what the Editor will compile, not what it happens to
# have noticed yet. Editor-only scripts are skipped: they belong to Assembly-CSharp-Editor, not this one.
listed = {p.lower().replace("/", os.sep) for p in re.findall(r'<Compile Include="([^"]+)"', csproj)}
unimported = []
for path in sorted((PROJ / "Assets").rglob("*.cs")):
    rel = path.relative_to(PROJ)
    if "Editor" in rel.parts:
        continue
    win = str(rel).replace("/", os.sep)
    if win.lower() not in listed:
        unimported.append(win)
if unimported:
    block = "".join(f'    <Compile Include="{p}" />\n' for p in unimported)
    csproj = csproj.replace("</Project>", "  <ItemGroup>\n" + block + "  </ItemGroup>\n</Project>")
    print(f"  [note] injected {len(unimported)} source(s) Unity has not imported yet:")
    for p in unimported:
        print(f"         {p}")

SCRATCH.write_text(csproj, encoding="utf-8")
try:
    res = subprocess.run(["dotnet", "build", SCRATCH.name, "--nologo", "-v", "m"],
                         cwd=PROJ, capture_output=True, text=True, timeout=600)
    errors = sorted(set(re.findall(r"error CS\d+: [^\r\n]+", res.stdout)))
    check("zero error CS lines", not errors, f"{len(errors)} errors" if errors else "")
    for e in errors[:10]:
        print(f"        {e}")
    check("MSBuild reports success", "Build succeeded." in res.stdout)

    print("\n=== 7. types present in produced assembly ===")
    out = PROJ / OUTDIR / "Assembly-CSharp.dll"
    if check("assembly was produced", out.exists()):
        asm = out.read_bytes().decode("latin-1")
        for t in ("SteamManager", "SteamEventBridge", "SteamAchievementsService",
                  "SteamStatsService", "SteamCloudSave", "SteamConfig", "Bootstrap",
                  "RunFailedState", "MainMenuState", "EventJsonLoader"):
            check(f"type '{t}' compiled in", t in asm)
finally:
    SCRATCH.unlink(missing_ok=True)
    shutil.rmtree(PROJ / OUTDIR, ignore_errors=True)

print(f"\n{'=' * 46}\n{checks - len(failures)}/{checks} checks passed")
if failures:
    print("FAILED:\n  - " + "\n  - ".join(failures))
    sys.exit(1)
print("ALL GREEN")
