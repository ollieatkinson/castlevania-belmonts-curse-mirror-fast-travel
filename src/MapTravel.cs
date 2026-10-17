using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using MotherBase.ApplicationFlowManager;
using TMPro;
using UnityEngine;

namespace MirrorFastTravel;

public sealed class MapTravel : MonoBehaviour
{
    internal static MapTravel? Instance;
    private TravelDestination? queued;
    private float deadline;
    private bool confirmationOpen;
    private UILocalizedString? footnote;
    private bool footnoteWasEnabled;
    private int lastSelection;
    private ConfirmationPopup? travelPopup;
    private UILocalizedString? confirmationTitle;
    private bool confirmationTitleWasEnabled;
    private string? confirmationTitleText;
    private TMP_Text? confirmationMessage;
    private bool messageWordWrapping;
    private bool messageAutoSizing;
    private float messageFontSize;
    private float messageFontSizeMin;
    private float messageFontSizeMax;

    public MapTravel(IntPtr pointer) : base(pointer) { }
    public void Awake() => Instance = this;
    public void OnDestroy()
    {
        RestoreFootnote();
        RestoreConfirmationTitle();
        Instance = null;
    }

    [HideFromIl2Cpp]
    internal void RestoreFootnote()
    {
        if (footnote) footnote!.enabled = footnoteWasEnabled;
        footnote = null;
    }

    [HideFromIl2Cpp]
    private void RestoreConfirmationTitle()
    {
        if (confirmationTitle) confirmationTitle!.enabled = confirmationTitleWasEnabled;
        if (confirmationMessage)
        {
            confirmationMessage!.enableWordWrapping = messageWordWrapping;
            confirmationMessage.enableAutoSizing = messageAutoSizing;
            confirmationMessage.fontSizeMin = messageFontSizeMin;
            confirmationMessage.fontSizeMax = messageFontSizeMax;
            confirmationMessage.fontSize = messageFontSize;
        }
        confirmationMessage = null;
        confirmationTitle = null;
        confirmationTitleText = null;
        travelPopup = null;
    }

    [HideFromIl2Cpp]
    private void RefreshConfirmationTitle()
    {
        if (travelPopup == null) return;
        if (!travelPopup || (!travelPopup!.visible && !confirmationOpen))
        {
            RestoreConfirmationTitle();
            return;
        }
        // Localized popup text can finish updating after Show. Keep our title
        // until this popup closes, then restore the component for other menus.
        if (confirmationTitle && confirmationTitle!.textDisplay
            && confirmationTitle.textDisplay.text != confirmationTitleText)
            confirmationTitle.textDisplay.text = confirmationTitleText;
        if (confirmationMessage)
        {
            // The stock prompt has whitespace before its question mark. Remove
            // that break opportunity, including a line break from localization.
            var text = confirmationMessage!.text;
            if (!string.IsNullOrEmpty(text) && text.EndsWith('?'))
            {
                var prompt = text[..^1].TrimEnd() + "?";
                if (prompt != text) confirmationMessage.text = prompt;
            }
        }
    }

    [HideFromIl2Cpp]
    internal void RefreshFootnote(UIMapLevelManager manager, LevelMapItem item, bool fromInit)
    {
        if (!item) return;
        var destination = Plugin.Travel.FindDestination(item);
        var selection = item.GetInstanceID();
        if (selection != lastSelection)
        {
            lastSelection = selection;
            Plugin.Logger.LogInfo($"Map selection: {item.GetIl2CppType().Name}, room={item.itemWorldRoomId}, linked={item.TryCast<LevelMapZDoorItem>()?.linkedWorldRoomUniqueID}, playerAvailable={MirrorTravel.TryGetPlayer(out _)}, travel={destination != null}");
        }
        if (destination == null) return;
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
    internal bool Confirm(TravelDestination destination)
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
            RestoreConfirmationTitle();
            travelPopup = ui.confirmationPopup;
            confirmationMessage = travelPopup.messageLabel?.textDisplay;
            if (confirmationMessage)
            {
                messageWordWrapping = confirmationMessage!.enableWordWrapping;
                messageAutoSizing = confirmationMessage.enableAutoSizing;
                messageFontSize = confirmationMessage.fontSize;
                messageFontSizeMin = confirmationMessage.fontSizeMin;
                messageFontSizeMax = confirmationMessage.fontSizeMax;
                confirmationMessage.enableWordWrapping = false;
                confirmationMessage.fontSizeMin = messageFontSize * 0.65f;
                confirmationMessage.fontSizeMax = messageFontSize;
                confirmationMessage.enableAutoSizing = true;
            }
            confirmationTitle = travelPopup.titleLabel;
            if (confirmationTitle && confirmationTitle!.textDisplay)
            {
                confirmationTitleWasEnabled = confirmationTitle.enabled;
                confirmationTitle.enabled = false;
                confirmationTitleText = destination.Label;
            }
            RefreshConfirmationTitle();
            Plugin.Logger.LogInfo($"Travel confirmation: {destination.Label}");
            return true;
        }
        catch
        {
            confirmationOpen = false;
            RestoreConfirmationTitle();
            throw;
        }
    }

    public void Update()
    {
        try
        {
            RefreshConfirmationTitle();
            Plugin.Travel.Update();
            if (queued == null) return;
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

[HarmonyPatch(typeof(LevelMapLandmark), nameof(LevelMapLandmark.Refresh))]
internal static class SelectDiscoveredLandmarks
{
    private static void Postfix(LevelMapLandmark __instance)
    {
        try { __instance.selectableItem = MirrorTravel.CanTravelToLandmark(__instance); }
        catch (Exception e) { Plugin.Logger.LogError($"Could not enable POI selection: {e}"); }
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

[HarmonyPatch(typeof(LevelMapZDoorItem), nameof(LevelMapZDoorItem.IsSelectableInternal))]
internal static class SelectVisitedFerryman
{
    private static void Postfix(LevelMapZDoorItem __instance, ref bool __result)
    {
        try { __result |= MirrorTravel.CanTravelToFerryman(__instance); }
        catch (Exception e) { Plugin.Logger.LogError($"Could not enable ferryman selection: {e}"); }
    }
}

[HarmonyPatch(typeof(LevelMapItem), nameof(LevelMapItem.IsSelectable))]
internal static class EnableVisitedFerryman
{
    private static void Prefix(LevelMapItem __instance)
    {
        try
        {
            var door = __instance.TryCast<LevelMapZDoorItem>();
            if (door && MirrorTravel.CanTravelToFerryman(door!)) door!.selectableItem = true;
        }
        catch (Exception e) { Plugin.Logger.LogError($"Could not enable ferryman map item: {e}"); }
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

[HarmonyPatch(typeof(LevelMapSavePointItem), nameof(LevelMapSavePointItem.Refresh))]
internal static class SelectVisitedMirrors
{
    private static void Postfix(LevelMapSavePointItem __instance)
    {
        try { __instance.selectableItem = MirrorTravel.WasVisited(__instance.itemWorldRoomId); }
        catch (Exception e) { Plugin.Logger.LogError($"Could not enable mirror selection: {e}"); }
    }
}

// HasUsedSavePoint and IsSelectableInternal share the same native function.
[HarmonyPatch(typeof(LevelMapSavePointItem), nameof(LevelMapSavePointItem.HasUsedSavePoint))]
internal static class AllowVisitedMirrorSelection
{
    private static void Postfix(LevelMapSavePointItem __instance, ref bool __result)
    {
        try { __result = MirrorTravel.WasVisited(__instance.itemWorldRoomId); }
        catch (Exception e) { Plugin.Logger.LogError($"Could not check visited mirror: {e}"); }
    }
}
