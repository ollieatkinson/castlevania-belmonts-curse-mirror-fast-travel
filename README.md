# Mirror Fast Travel

A BepInEx mod for Castlevania: Belmont's Curse that lets you fast travel from anywhere to visited Mirrors of Life.

**Early prototype.** Uses the visited mirrors in your existing save; no need to revisit them after installing.

## Install

Requires [BepInEx 6 for Unity IL2CPP, Windows x64](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html). Run the game with BepInEx once before building or installing.

Download the ZIP from [Releases](https://github.com/ollieatkinson/castlevania-belmonts-curse-mirror-fast-travel/releases) and extract it into the game directory. It places `MirrorFastTravel.dll` in `BepInEx/plugins/`.

During normal gameplay, open the **area map** from anywhere and select a visited mirror. Use the map's normal interact button when **Travel** appears, then confirm in the game's popup. Cancelling leaves you on the map.

The mod reads exploration from the current save and finds the mirror in the destination room. It uses the game's saving and level-loading code for travel. It does not unlock undiscovered mirrors or change game files.

To uninstall, close the game and delete `BepInEx/plugins/MirrorFastTravel.dll`.

## Build

Install the .NET 8 SDK, then build against your game's generated BepInEx assemblies:

```sh
dotnet build -c Release -p:GamePath="/path/to/Castlevania Belmont's Curse"
```

The DLL is written to `bin/Release/net6.0/`. Game and BepInEx assemblies are references only; they are not bundled with the mod.

## Package and release

With Bash, Python 3, and the .NET 8 SDK installed, build a ZIP and SHA-256 checksum locally:

```sh
GamePath="/path/to/Castlevania Belmont's Curse" bash scripts/package.sh
```

Files are written to `dist/`. The ZIP contains the plugin under `BepInEx/plugins/`, this README, and the licence.

To publish a version, install the [GitHub CLI](https://cli.github.com/), authenticate with `gh auth login`, and commit and push your changes. Then run:

```sh
GamePath="/path/to/Castlevania Belmont's Curse" bash scripts/release.sh v0.1.0
```

The script builds and packages the mod, creates a tag for the current commit, and publishes a GitHub release with the ZIP, checksum, and generated release notes. The version sets both the assembly version and the BepInEx plugin version. Tags such as `v0.2.0-preview.1` create prereleases.

For future releases, pass the next version to the release script. Update `<Version>` in `MirrorFastTravel.csproj` when you want to change the default version used by local builds.
