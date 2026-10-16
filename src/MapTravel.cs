using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using MotherBase.ApplicationFlowManager;
using UnityEngine;

namespace MirrorFastTravel;

public sealed class MapTravel : MonoBehaviour
{
    internal static MapTravel? Instance;
    private Mirror? queued;
    private float deadline;
    private bool confirmationOpen;
    private UILocalizedString? footnote;
    private bool footnoteWasEnabled;

    public MapTravel(IntPtr pointer) : base(pointer) { }
    public void Awake() => Instance = this;
    public void OnDestroy()
    {
        RestoreFootnote();
        Instance = null;
    }

    [HideFromIl2Cpp]
    internal void RestoreFootnote()
    {
        if (footnote) footnote!.enabled = footnoteWasEnabled;
        footnote = null;
    }

    [HideFromIl2Cpp]
    internal void RefreshFootnote(UIMapLevelManager manager, LevelMapItem item, bool fromInit)
    {
        if (Plugin.Travel.FindDestination(item) == null) return;
        var text = manager.interactFootnoteText;
        if (!text || !text.textDisplay) return;
        footnote = text;
        footnoteWasEnabled = text.enabled;
        text.enabled = false;
        text.textDisplay.text = "Travel";
        var view = fromInit ? manager.hasInteractFootnoteInitBooleanView : manager.hasInteractFootnoteBooleanView;
        if (view) view.SetBool(true);
    }

    [HideFromIl2Cpp]
    internal bool Confirm(Mirror destination)
    {
        if (confirmationOpen || queued != null) return false;
        var ui = UIManager.GetInstance();
        if (!ui || !ui.fastTravelMenuManager || !ui.confirmationPopup || ui.confirmationPopup.visible)
            return false;

        confirmationOpen = true;
        try
        {
            ui.OpenConfirmPopup(ui.fastTravelMenuManager.confirmationPopupTexts,
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() => confirmationOpen = false)),
                DelegateSupport.ConvertDelegate<Il2CppSystem.Action>((Action)(() =>
                {
                    confirmationOpen = false;
                    try
                    {
                        if (!ui.TryGetMenuManager(out var menu) || !menu) return;
                        queued = destination;
                        deadline = Time.realtimeSinceStartup + 10f;
                        menu.Close();
                    }
                    catch (Exception e)
                    {
                        queued = null;
                        Plugin.Logger.LogError($"Could not close the map for mirror travel: {e}");
                    }
                })));
            return true;
        }
        catch
        {
            confirmationOpen = false;
            throw;
        }
    }

    public void Update()
    {
        if (queued == null) return;
        try
        {
            if (Time.realtimeSinceStartup > deadline || ApplicationFlowManager.IsLoading)
            {
                queued = null;
                Plugin.Logger.LogWarning("Mirror travel cancelled: the map did not close or another load started.");
                return;
            }
            var ui = UIManager.GetInstance();
            if (!ui || !ui.TryGetMenuManager(out var menu) || menu.IsShowing() || ui.confirmationPopup.visible
                || !GameStateManager.GetInstance().IsGameplayState()) return;
            var target = queued;
            queued = null;
            Plugin.Travel.Start(target);
        }
        catch (Exception e)
        {
            queued = null;
            Plugin.Logger.LogError($"Could not start mirror travel: {e}");
        }
    }
}

[HarmonyPatch(typeof(LevelMapController), nameof(LevelMapController.UpdateCursorControls))]
internal static class InteractWithMirror
{
    private static bool Prefix(LevelMapController __instance)
    {
        try
        {
            if (__instance._importantInputProcessedForFrame || !__instance.AreControlsEnabled()
                || !__instance.interactInput || !__instance.interactInput.action.WasPerformedThisFrame()) return true;
            var destination = Plugin.Travel.FindDestination(__instance.cursor.GetCurrentSelection());
            if (destination == null || MapTravel.Instance == null) return true;
            if (!MapTravel.Instance.Confirm(destination)) return true;
            __instance._importantInputProcessedForFrame = true;
            return false;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Could not open mirror travel: {e}");
            return true;
        }
    }
}

[HarmonyPatch(typeof(UIMapLevelManager), nameof(UIMapLevelManager.RefreshInteractFootnoteGroup))]
internal static class MirrorInteractionPrompt
{
    private static void Prefix() => MapTravel.Instance?.RestoreFootnote();

    private static void Postfix(UIMapLevelManager __instance, LevelMapItem __0, bool __1)
    {
        try { MapTravel.Instance?.RefreshFootnote(__instance, __0, __1); }
        catch (Exception e) { Plugin.Logger.LogError($"Could not show mirror travel prompt: {e}"); }
    }
}

[HarmonyPatch(typeof(UIMapLevelManager), nameof(UIMapLevelManager.OnDisable))]
internal static class RestoreMapPrompt
{
    private static void Prefix() => MapTravel.Instance?.RestoreFootnote();
}
