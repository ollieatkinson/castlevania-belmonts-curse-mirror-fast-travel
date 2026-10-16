using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MotherBase.EntityFramework;
using UnityEngine;

namespace MirrorFastTravel;

[BepInPlugin(Id, "Mirror Fast Travel", "0.1.0")]
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
        AddComponent<MirrorMenu>();
        Log.LogInfo("Mirror Fast Travel 0.1.0 loaded. Use a mirror to record it; F8 opens travel nearby.");
    }
}

[HarmonyPatch(typeof(SaveLogic), nameof(SaveLogic.Save),
    new[] { typeof(Entity), typeof(Vector3), typeof(SaveOrigin) })]
internal static class RecordMirror
{
    private static void Postfix(Entity __0, Vector3 __1, SaveOrigin __2)
    {
        if (__2 != SaveOrigin.SavePoint) return;
        try { Plugin.Travel.Record(__0, __1); }
        catch (Exception e) { Plugin.Logger.LogError($"Could not record mirror: {e}"); }
    }
}

[HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.Initialize))]
internal static class PrepareArrival
{
    private static void Prefix(MainCharacterSpawnLogic __instance)
    {
        try { Plugin.Travel.PrepareArrival(__instance); }
        catch (Exception e) { Plugin.Logger.LogError($"Could not prepare mirror arrival: {e}"); }
    }
}

[HarmonyPatch(typeof(MainCharacterSpawnLogic), nameof(MainCharacterSpawnLogic.OnAFMEndLoadState))]
internal static class CheckArrival
{
    private static void Postfix()
    {
        try { Plugin.Travel.CheckArrival(); }
        catch (Exception e) { Plugin.Logger.LogError($"Could not verify mirror arrival: {e}"); }
    }
}
