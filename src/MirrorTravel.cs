using MotherBase.ApplicationFlowManager;
using MotherBase.EntityFramework;
using MotherBase.RoomDesigner;
using RedWine.ResourceManagement;
using RoomManager;
using UnityEngine;

namespace MirrorFastTravel;

internal sealed record Mirror(string RoomId, string LevelGuid, string Label, Vector3 RoomCenter);

internal sealed class MirrorTravel
{
    private Mirror? pending;
    private Vector3? arrival;
    private bool returnToSave;

    internal static bool NearMirror(out Entity player)
    {
        player = null!;
        if (ApplicationFlowManager.IsLoading || !MainCharacterSingleton.TryGetEntity(out player) || !player)
            return false;
        var room = LevelHelpers.GetCurrentRoom(player);
        if (!room || room.worldRoomData?.roomData?.configuration?.TryCast<RedWineRoomConfiguration>()?.isSaveRoom != true)
            return false;
        foreach (var mirror in room.GetComponentsInChildren<SavePoint>(true))
            if (mirror.isActiveAndEnabled && Vector2.Distance(player.transform.position, mirror.transform.position) < 6f)
                return true;
        return false;
    }

    internal static bool WasVisited(string roomId)
    {
        var explorer = MainCharacterSingleton.GetComponent<ExplorerComponent>();
        return explorer && explorer.worldRoomExplorationStates.TryGetValue(roomId, out var state)
            && state != null && state.isSaveRoom
            && (state.usedRoomSavePoint || state.ExploredAtLeastOneChunk() || state.enteredZonesIds?.Count > 0);
    }

    internal Mirror? FindDestination(LevelMapItem? item)
    {
        if (!item || pending != null || returnToSave || !NearMirror(out _)) return null;
        var door = item!.TryCast<LevelMapZDoorItem>();
        if (!door && !item.TryCast<LevelMapSavePointItem>() && !item.TryCast<LevelMapLandmark>()) return null;
        var roomId = door ? door!.linkedWorldRoomUniqueID : item.itemWorldRoomId;
        if (string.IsNullOrEmpty(roomId) || !WasVisited(roomId)) return null;
        var level = item.context?.level;
        var manager = LevelManager.Get();
        if (!level || !manager || !manager.worldDataDictionary.TryGetItem(level, out var reference)) return null;
        var world = ResourceManager.LoadAssetByRef<WorldData>(ResourceContext.UI_GAMEMENU, reference);
        if (!world) return null;
        foreach (var room in world.GetRooms())
        {
            if (room.id != roomId || room.roomData.configuration.TryCast<RedWineRoomConfiguration>()?.isSaveRoom != true)
                continue;
            return new Mirror(roomId, level!.guid, room.instanceName, RoomDesignerExtensions.GetRoomCenterPosition(room));
        }
        return null;
    }

    private static LevelID? FindLevel(string guid)
    {
        var manager = LevelManager.Get();
        if (!manager) return null;
        foreach (var element in manager.applicationStateDictionary.elements)
            if (element.id && element.id.guid == guid) return element.id;
        return null;
    }

    internal void Start(Mirror destination)
    {
        if (pending != null || returnToSave || !NearMirror(out var player) || !GameStateManager.GetInstance().IsGameplayState())
            throw new InvalidOperationException("Travel is only available beside a mirror during normal gameplay.");
        var level = FindLevel(destination.LevelGuid);
        if (!WasVisited(destination.RoomId) || !level)
            throw new InvalidOperationException("This mirror has not been visited in the current save.");

        SaveLogic.Get().SaveNoPosition(player, SaveOrigin.Flow);
        pending = destination;
        arrival = null;
        var spawn = MainCharacterSpawnLogic.Get();
        // This only selects the room to stream. Place the player at its mirror
        // before OnFirstRoomLoaded starts the room's camera and gameplay.
        spawn.SetForcePosition(destination.RoomCenter, null);
        try
        {
            if (!LevelManager.Get().TryLoadLevelState(level, MainGameLoadingType.FastTravel, null))
                throw new InvalidOperationException("The game refused the level transition.");
            Plugin.Logger.LogInfo($"Travelling to visited mirror {destination.RoomId}");
        }
        catch
        {
            pending = null;
            spawn.ClearForcePosition();
            throw;
        }
    }

    internal void PrepareArrival(MainCharacterSpawnLogic spawn)
    {
        if (pending == null) return;
        var level = LevelManager.Get().loadingLevelVariable.CurrentValue;
        if (!level || level.guid != pending.LevelGuid)
        {
            pending = null;
            spawn.ClearForcePosition();
            return;
        }
        spawn.SetForcePosition(pending.RoomCenter, null);
    }

    internal void PlaceAtMirror(RoomManagerSystem manager, RoomStreamingHandle handle, ref RoomChunkID chunk)
    {
        if (pending == null) return;
        if (handle.worldRoomData.id != pending.RoomId || !handle.roomInstance)
            throw new InvalidOperationException("The loaded room does not match the selected mirror.");
        var mirrors = handle.roomInstance.GetComponentsInChildren<SavePoint>(true);
        if (mirrors.Length != 1)
            throw new InvalidOperationException($"Expected one mirror in {pending.RoomId}, found {mirrors.Length}.");
        var player = MainCharacterSingleton.GetEntity();
        var position = mirrors[0].transform.position;
        // SavePoint's root is at the player's feet; artwork is offset into depth.
        position.z = player.transform.position.z;
        position.y += 0.01f;
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
            throw new InvalidOperationException("The mirror has an invalid position.");
        MainCharacterSpawnLogic.Get().PlaceMainCharacter(position, player.transform.rotation);
        if (!manager.GetEntityRoomAndChunk(player.gameObject, out var room, out var mirrorChunk, out _)
            || room.worldRoomData.id != pending.RoomId)
            throw new InvalidOperationException("The mirror's position is outside the destination room.");
        chunk = mirrorChunk;
        arrival = position;
        Plugin.Logger.LogInfo($"Placed player at {pending.RoomId}: {position}");
    }

    internal void FailArrival(Exception error)
    {
        Plugin.Logger.LogError($"Mirror travel failed; returning to the saved mirror: {error}");
        pending = null;
        arrival = null;
        MainCharacterSpawnLogic.Get().ClearForcePosition();
        returnToSave = true;
    }

    internal void CheckArrival()
    {
        if (pending == null) return;
        var destination = pending;
        var player = MainCharacterSingleton.GetEntity();
        var roomId = LevelHelpers.GetCurrentRoom(player)?.worldRoomData?.id;
        if (arrival == null || roomId != destination.RoomId || Vector3.Distance(player.transform.position, arrival.Value) > 3f)
        {
            FailArrival(new InvalidOperationException($"Expected arrival in {destination.RoomId}, got {roomId}."));
            return;
        }
        pending = null;
        arrival = null;
        Plugin.Logger.LogInfo($"Arrived at visited mirror {destination.RoomId}");
    }

    internal void Update()
    {
        if (!returnToSave || ApplicationFlowManager.IsLoading) return;
        returnToSave = false;
        LevelManager.Get().ReloadSaveState(MainGameLoadingType.FastTravel);
    }
}
