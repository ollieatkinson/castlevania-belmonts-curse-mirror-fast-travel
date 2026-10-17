using BepInEx;
using BepInEx.Logging;
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

    public override void Load()
    {
        Logger = Log;
        Travel = new MirrorTravel();
        new Harmony(Id).PatchAll(typeof(Plugin).Assembly);
        AddComponent<MapTravel>();
        Log.LogInfo($"Mirror Fast Travel {BuildInfo.Version} loaded. Select a visited mirror on the area map to travel.");
    }
}

[HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.Initialize))]
internal static class PrepareArrival
{
    private static void Prefix(MainCharacterSpawnLogic __instance)
    {
        try { Plugin.Travel.PrepareArrival(__instance); }
        catch (Exception e) { Plugin.Travel.FailArrival(e); }
    }
}

[HarmonyPatch(typeof(RoomManagerSystem), nameof(RoomManagerSystem.OnFirstRoomLoaded))]
internal static class PlaceAtMirror
{
    private static void Prefix(RoomManagerSystem __instance, RoomStreamingHandle __0, ref RoomChunkID __1)
    {
        try { Plugin.Travel.PlaceAtMirror(__instance, __0, ref __1); }
        catch (Exception e) { Plugin.Travel.FailArrival(e); }
    }
}

[HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.OnAFMEndLoadState))]
internal static class CheckArrival
{
    private static void Postfix()
    {
        try { Plugin.Travel.CheckArrival(); }
        catch (Exception e) { Plugin.Travel.FailArrival(e); }
    }
}
