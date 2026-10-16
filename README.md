# Mirror Fast Travel

A BepInEx mod for Castlevania: Belmont's Curse that lets you travel between visited Mirrors of Life.

**Early prototype.** Uses the visited mirrors in your existing save; no need to revisit them after installing.

## Install

Requires [BepInEx 6 for Unity IL2CPP, Windows x64](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html). Run the game with BepInEx once before building or installing.

Copy `MirrorFastTravel.dll` into `BepInEx/plugins/`.

While standing beside a mirror, open the **area map** and select a visited mirror. Use the map's normal interact button when **Travel** appears, then confirm in the game's popup. Cancelling leaves you on the map.

The mod reads exploration from the current save and finds the mirror in the destination room. It uses the game's saving and level-loading code for travel. It does not unlock undiscovered mirrors or change game files.

To uninstall, close the game and delete `BepInEx/plugins/MirrorFastTravel.dll`.

## Build

Install the .NET 8 SDK, then build against your game's generated BepInEx assemblies:

```sh
dotnet build -c Release -p:GamePath="/path/to/Castlevania Belmont's Curse"
```

The DLL is written to `bin/Release/net6.0/`. Game and BepInEx assemblies are references only; they are not bundled with the mod.
