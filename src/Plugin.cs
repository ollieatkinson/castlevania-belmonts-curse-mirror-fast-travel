using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using RoomManager;

namespace MirrorFastTravel;

[BepInPlugin(Id, "Mirror Fast Travel", BuildInfo.Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "ollieatkinson.mirrorfasttravel";
    internal static ManualLogSource Logger = null!;
    internal static MirrorTravel Travel = null!;
    internal static ConfigEntry<bool> DiagnosticsEnabled = null!;

    public override void Load()
    {
        Logger = Log;
        DiagnosticsEnabled = Config.Bind("Diagnostics", "Enabled", true,
            "Log transition, spawn, menu and camera state, including 1, 5 and 15 seconds after loading. Disable after troubleshooting.");
        Travel = new MirrorTravel();
        new Harmony(Id).PatchAll(typeof(Plugin).Assembly);
        AddComponent<MapTravel>();
        Log.LogInfo($"Mirror Fast Travel {BuildInfo.Version} loaded. Select a visited mirror, discovered POI, or visited ferryman on the area map to travel.");
        Log.LogInfo($"Travel diagnostics enabled={DiagnosticsEnabled.Value}. Unity log path: {UnityEngine.Application.consoleLogPath}");
    }
}

[HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.Initialize))]
internal static class PrepareArrival
{
    private static void Prefix(MainCharacterSpawnLogic __instance)
    {
        TravelDiagnostics.BeginLoad();
        try { Plugin.Travel.PrepareArrival(__instance); }
        catch (Exception e) { Plugin.Travel.FailArrival(e); }
    }

    private static void Finalizer(Exception? __exception) => TravelDiagnostics.LogException("spawn-initialize", __exception);
}

[HarmonyPatch(typeof(RoomManagerSystem), nameof(RoomManagerSystem.OnFirstRoomLoaded))]
internal static class PlaceAtDestination
{
    private static void Prefix(RoomManagerSystem __instance, RoomStreamingHandle __0, ref RoomChunkID __1)
    {
        TravelDiagnostics.Snapshot("first-room-before");
        try { Plugin.Travel.PlaceAtDestination(__instance, __0, ref __1); }
        catch (Exception e) { Plugin.Travel.FailArrival(e); }
    }

    private static void Postfix() => TravelDiagnostics.Snapshot("first-room-after");

    private static void Finalizer(Exception? __exception) => TravelDiagnostics.LogException("first-room", __exception);
}

[HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.OnAFMEndLoadState))]
internal static class CheckArrival
{
    private static void Prefix() => TravelDiagnostics.Snapshot("load-end-before");

    private static void Postfix()
    {
        try { Plugin.Travel.CheckArrival(); }
        catch (Exception e) { Plugin.Travel.FailArrival(e); }
        finally { TravelDiagnostics.FinishLoad(); }
    }

    private static void Finalizer(Exception? __exception) => TravelDiagnostics.LogException("load-end", __exception);
}
