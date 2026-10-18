# Mirror Fast Travel

A BepInEx mod for Castlevania: Belmont's Curse that lets you fast travel from anywhere to visited Mirrors of Life, discovered points of interest (POIs), and visited ferryman rooms.

**Early prototype.** Uses exploration in your existing save; no need to rediscover destinations after installing. Version 0.3.0 adds POI and ferryman travel, plus standalone downloads with BepInEx included.

Version 0.3.1 fixes ferryman selection, shows the destination and area in confirmation titles, and keeps the confirmation question on one line.

Version 0.3.2-preview.1 adds precautionary transition fixes and diagnostics for intermittent black screens. It clears the mod's forced spawn position after arrival and waits for the map to remain closed for at least 0.2 seconds and two frames before starting travel. This is a test build; the reported black screen has not yet been reproduced or confirmed fixed.

## Install

Download one ZIP from [Releases](https://github.com/ollieatkinson/castlevania-belmonts-curse-mirror-fast-travel/releases):

- **`.standalone.zip`** includes BepInEx 6.0.788 for Unity IL2CPP, Windows x64. Use this for a fresh install.
- **`.zip`** contains only the mod. Use this if BepInEx is already installed.

Close the game and extract your chosen ZIP into the game directory, beside the game executable. It places `MirrorFastTravel.dll` in `BepInEx/plugins/`. The first launch with BepInEx may take a few minutes while it generates game assemblies.

On Linux/Proton, add `WINEDLLOVERRIDES="winhttp=n,b" %command%` to the game's Steam launch options. Keep any other launch options you need.

During normal gameplay, open the **area map** from anywhere and select a visited mirror, discovered POI, or the door icon for a visited ferryman room. Use the map's normal interact button when **Travel** appears, then confirm in the game's popup. Cancelling leaves you on the map.

POIs are the game's named landmarks in rooms you have visited. For POIs and ferryman rooms, the mod looks for a clear standing position on solid ground near the destination. If it cannot find one, it returns you to your saved mirror. Custom map markers and the last death location are not travel destinations.

The mod reads exploration from the current save and finds the mirror, landmark, or ferryman in the destination room. It uses the game's saving and level-loading code for travel. It does not unlock undiscovered destinations or change game files.

To uninstall, close the game and delete `BepInEx/plugins/MirrorFastTravel.dll`.

## Black-screen troubleshooting

You can test with any visited mirror; no particular Hall of Mirrors destination is required. Travel between two available mirrors several times, then try opening and closing the menu after arrival. For compatibility testing, restart with Mirror Fast Travel as the only plugin, then add other plugins one at a time.

The preview logs transition stages, player and spawn state, menu visibility, and camera state in `BepInEx/LogOutput.log`. It also records camera state 1, 5, and 15 seconds after loading, including ordinary game transitions. If a black screen occurs, wait at least 15 seconds if possible, then close the game and copy the log before restarting. Include the origin, destination, other installed mods, and whether Rose still moves or the menu leaves trails. An `Arrived` message verifies player placement only; it does not verify rendering.

The startup log prints Unity's reported log path, which may help locate `Player.log` when it is available. Diagnostics work in the BepInEx log without a separate Unity log. To disable the additional snapshots after testing, set `Enabled = false` under `[Diagnostics]` in `BepInEx/config/ollieatkinson.mirrorfasttravel.cfg` while the game is closed.

## Build

Install the .NET 8 SDK and run the game with BepInEx once, then build against the generated assemblies:

```sh
dotnet build -c Release -p:GamePath="/path/to/Castlevania Belmont's Curse"
```

The DLL is written to `bin/Release/net6.0/`. Game assemblies are references only and are never included in either download.

Run the map-close timing regression checks without game assemblies:

```sh
dotnet run --project tests/TravelStartGate.Tests.csproj -c Release
```

## Package and release

With Bash, Python 3, and the .NET 8 SDK installed, build both ZIPs and their SHA-256 checksums locally:

```sh
GamePath="/path/to/Castlevania Belmont's Curse" bash scripts/package.sh
```

Files are written to `dist/`. The plugin-only ZIP contains the plugin under `BepInEx/plugins/`, this README, and the licence. The standalone ZIP adds the pinned BepInEx 788 distribution, dependency licenses, and corresponding sources under `MirrorFastTravel/licenses/`. Downloads are cached in `.build/` and verified against SHA-256 hashes before packaging.

To create a standalone ZIP from an existing plugin ZIP without rebuilding the mod:

```sh
dotnet msbuild -t:PackageStandalone -p:Version=0.2.0 -p:PluginArchive="/path/to/MirrorFastTravel-v0.2.0.zip"
```

To publish a version, install the [GitHub CLI](https://cli.github.com/), authenticate with `gh auth login`, and commit and push your changes. Then run:

```sh
GamePath="/path/to/Castlevania Belmont's Curse" bash scripts/release.sh v0.1.0
```

The script builds both packages, creates a tag for the current commit, and publishes a GitHub release with both ZIPs, their checksums, and generated release notes. The version sets both the assembly version and the BepInEx plugin version. Tags such as `v0.2.0-preview.1` create prereleases.

For future releases, pass the next version to the release script. Update `<Version>` in `MirrorFastTravel.csproj` when you want to change the default version used by local builds.
