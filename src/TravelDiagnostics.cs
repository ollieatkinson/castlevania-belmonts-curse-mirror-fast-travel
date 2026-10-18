using MotherBase.ApplicationFlowManager;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorFastTravel;

internal static class TravelDiagnostics
{
    private static readonly float[] checkpoints = { 1f, 5f, 15f };
    private static int load;
    private static float? loadStarted;
    private static float? loadFinished;
    private static int checkpoint;
    private static bool warned;

    internal static void BeginLoad()
    {
        if (!Plugin.DiagnosticsEnabled.Value) return;
        load++;
        loadStarted = Time.realtimeSinceStartup;
        loadFinished = null;
        warned = false;
        Snapshot("spawn-initialize-before");
    }

    internal static void FinishLoad()
    {
        if (!Plugin.DiagnosticsEnabled.Value) return;
        loadFinished = Time.realtimeSinceStartup;
        checkpoint = 0;
        Snapshot("load-end-after");
    }

    internal static void Update()
    {
        if (!Plugin.DiagnosticsEnabled.Value) return;
        var now = Time.realtimeSinceStartup;
        if (loadStarted != null && loadFinished == null && !warned && now - loadStarted.Value >= 30f)
        {
            warned = true;
            Plugin.Logger.LogWarning($"Travel diagnostics load={load}: no load-end callback observed after 30 seconds; no automatic recovery attempted.");
            Snapshot("load-end-timeout");
        }
        if (loadFinished == null || checkpoint >= checkpoints.Length
            || now - loadFinished.Value < checkpoints[checkpoint]) return;
        Snapshot($"after-load-{checkpoints[checkpoint]}s");
        checkpoint++;
    }

    internal static void LogException(string stage, Exception? error)
    {
        if (error == null) return;
        Plugin.Logger.LogError($"Game transition threw during {stage}: {error}");
        Snapshot($"exception-{stage}");
    }

    // Diagnostic failures must never interrupt the game's loading callbacks.
    internal static void Snapshot(string stage)
    {
        if (!Plugin.DiagnosticsEnabled.Value) return;
        try
        {
            var tag = $"Travel diagnostics load={load}, stage={stage}, frame={Time.frameCount}, time={Time.realtimeSinceStartup:F2}";
            var state = GameStateManager.GetInstance();
            var spawn = MainCharacterSpawnLogic.Get();
            var level = LevelManager.Get();
            var loadingLevel = level ? level.loadingLevelVariable?.CurrentValue?.guid : null;
            var playerAvailable = MainCharacterSingleton.TryGetEntity(out var player) && player;
            var playerState = playerAvailable
                ? $"player={player.transform.position}, room={LevelHelpers.GetCurrentRoom(player)?.worldRoomData?.id}"
                : "player=unavailable";
            Plugin.Logger.LogInfo($"{tag}: loading={ApplicationFlowManager.IsLoading}, level={loadingLevel}, gameplay={state && state.IsGameplayState()}, timeScale={Time.timeScale}, scene={SceneManager.GetActiveScene().name}, {playerState}");
            if (spawn)
                Plugin.Logger.LogInfo($"{tag}: forceSpawn={spawn._forceSpawnPosition}, spawnPosition={spawn._spawnPosition}, useSpawner={spawn.forceUseCharacterSpawner}");
            var ui = UIManager.GetInstance();
            if (ui)
            {
                var menuAvailable = ui.TryGetMenuManager(out var menu) && menu;
                Plugin.Logger.LogInfo($"{tag}: menuAvailable={menuAvailable}, menuShowing={menuAvailable && menu.IsShowing()}, confirmationVisible={ui.confirmationPopup && ui.confirmationPopup.visible}");
            }

            // Include disabled cameras: a disabled world camera is one of the suspected failures.
            var cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            var main = Camera.main;
            Plugin.Logger.LogInfo($"{tag}: cameras={cameras.Length}, mainCamera={(main ? main.GetInstanceID().ToString() : "none")}");
            foreach (var camera in cameras)
            {
                if (!camera) continue;
                try
                {
                    var target = camera.targetTexture;
                    var output = target
                        ? $"texture:{target.name}#{target.GetInstanceID()}({target.width}x{target.height},created={target.IsCreated()})"
                        : "display";
                    Plugin.Logger.LogInfo($"{tag}: camera={camera.name}#{camera.GetInstanceID()}, scene={camera.gameObject.scene.name}, enabled={camera.enabled}, active={camera.gameObject.activeInHierarchy}, main={camera == main}, clear={camera.clearFlags}, mask=0x{camera.cullingMask:X8}, depth={camera.depth}, output={output}, display={camera.targetDisplay}, rect={camera.rect}, position={camera.transform.position}, rotation={camera.transform.eulerAngles}, ortho={camera.orthographic}, orthoSize={camera.orthographicSize}, clips={camera.nearClipPlane}/{camera.farClipPlane}");
                }
                catch (Exception error)
                {
                    Plugin.Logger.LogWarning($"{tag}: could not inspect a camera: {error.Message}");
                }
            }
        }
        catch (Exception error)
        {
            Plugin.Logger.LogWarning($"Could not capture travel diagnostics ({stage}): {error.Message}");
        }
    }
}
