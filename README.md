# Mirror Fast Travel

A BepInEx mod for Castlevania: Belmont's Curse that lets you travel between visited Mirrors of Life.

**Early prototype — not yet verified in-game.** For now, use each mirror once with the mod installed to record its arrival position. Mirrors you visited before installing the mod need another visit. Destinations are checked against the current save, so recording a mirror in one playthrough does not unlock it in another.

## Install

Requires [BepInEx 6 for Unity IL2CPP, Windows x64](https://docs.bepinex.dev/master/articles/user_guide/installation/unity_il2cpp.html). Run the game with BepInEx once before building or installing.

Copy `MirrorFastTravel.dll` into `BepInEx/plugins/`.

Use a mirror normally, then press **F8** beside it to open the travel list. Choose another recorded mirror with the mouse. **F8** or **Escape** closes the list. The game pauses while the list is open.

Arrival positions are stored in `BepInEx/config/ollieatkinson.mirrorfasttravel.mirrors.json`. The mod uses the game's saving and level-loading code for travel. It does not unlock undiscovered mirrors or change game files.

To uninstall, close the game and delete `BepInEx/plugins/MirrorFastTravel.dll`. You can also delete the mod's JSON file.

## Build

Install the .NET 8 SDK, then build against your game's generated BepInEx assemblies:

```sh
dotnet build -c Release -p:GamePath="/path/to/Castlevania Belmont's Curse"
```

The DLL is written to `bin/Release/net6.0/`. Game and BepInEx assemblies are references only; they are not bundled with the mod.
