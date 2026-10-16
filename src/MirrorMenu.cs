using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MirrorFastTravel;

public sealed class MirrorMenu : MonoBehaviour
{
    private List<Mirror>? destinations;
    private Mirror? selected;
    private EditorParameteredGameState? menuState;
    private GameStateManager? manager;
    private int stateId = -1;
    private int page;
    private bool oldCursorVisible;
    private CursorLockMode oldCursorLock;
    private string message = "";
    private float messageUntil;

    public MirrorMenu(IntPtr pointer) : base(pointer) { }

    public void Update()
    {
        try
        {
            if (!Application.isFocused) return;
            var keyboard = Keyboard.current;
            if (destinations != null && keyboard?.escapeKey.wasPressedThisFrame == true) Close();
            if (keyboard?.f8Key.wasPressedThisFrame == true)
            {
                if (destinations != null) Close();
                else Open();
            }
            if (selected != null)
            {
                var target = selected;
                selected = null;
                Close();
                Plugin.Travel.Start(target);
            }
        }
        catch (Exception e)
        {
            Close();
            message = e.Message;
            messageUntil = Time.unscaledTime + 8f;
            Plugin.Logger.LogError(e);
        }
    }

    [HideFromIl2Cpp]
    private void Open()
    {
        manager = GameStateManager.GetInstance();
        if (Plugin.Travel.IsTravelling || !manager || !manager.IsGameplayState() || !MirrorTravel.NearMirror(out _))
        {
            message = "Stand beside a Mirror of Life to travel.";
            messageUntil = Time.unscaledTime + 4f;
            return;
        }
        destinations = Plugin.Travel.GetDestinations();
        page = 0;
        oldCursorVisible = Cursor.visible;
        oldCursorLock = Cursor.lockState;
        menuState = ScriptableObject.CreateInstance<EditorParameteredGameState>();
        menuState.name = "MirrorFastTravelMenu";
        menuState._pauseEntityContext = true;
        menuState._pausePlaytimeCount = true;
        menuState._pauseCutsceneManager = true;
        menuState._disableMenus = true;
        menuState._disableDebugBindings = true;
        menuState._activeActionMaps = new Il2CppStringArray(0);
        stateId = manager.PushState(menuState, this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    [HideFromIl2Cpp]
    private void Close()
    {
        if (destinations == null) return;
        destinations = null;
        if (manager && stateId >= 0) manager!.PopState(stateId, this);
        stateId = -1;
        if (menuState) Destroy(menuState);
        menuState = null;
        Cursor.lockState = oldCursorLock;
        Cursor.visible = oldCursorVisible;
    }

    public void OnDestroy() => Close();

    public void OnGUI()
    {
        var previous = GUI.matrix;
        var scale = Math.Max(1f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
        try { Draw(Screen.width / scale, Screen.height / scale); }
        finally { GUI.matrix = previous; }
    }

    [HideFromIl2Cpp]
    private void Draw(float screenWidth, float screenHeight)
    {
        if (Time.unscaledTime < messageUntil)
            GUI.Box(new Rect((screenWidth - 600) / 2f, 40, 600, 55), message);
        if (destinations == null) return;
        const float width = 620, height = 475;
        var x = (screenWidth - width) / 2f;
        var y = (screenHeight - height) / 2f;
        GUI.Box(new Rect(x, y, width, height), "Mirror of Life — Travel");
        GUI.Label(new Rect(x + 20, y + 35, width - 40, 40),
            "Choose a visited mirror. F8 or Escape closes this window.");
        if (destinations.Count == 0)
            GUI.Label(new Rect(x + 20, y + 90, width - 40, 80),
                "No other mirrors recorded yet.\nUse another mirror once with this mod installed.");
        for (var i = page * 7; i < Math.Min(destinations.Count, (page + 1) * 7); i++)
            if (GUI.Button(new Rect(x + 20, y + 85 + (i % 7) * 44, width - 40, 38), destinations[i].Label))
                selected = destinations[i];
        if (page > 0 && GUI.Button(new Rect(x + 20, y + 410, 110, 40), "Previous")) page--;
        if ((page + 1) * 7 < destinations.Count && GUI.Button(new Rect(x + 145, y + 410, 110, 40), "Next")) page++;
        if (GUI.Button(new Rect(x + width - 130, y + 410, 110, 40), "Close")) Close();
    }
}
