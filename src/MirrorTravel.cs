using MotherBase.ApplicationFlowManager;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using MotherBase.RoomDesigner;
using RedWine.ResourceManagement;
using RoomManager;
using UnityEngine;

namespace MirrorFastTravel;

internal sealed record TravelDestination(string RoomId, string LevelGuid, string Label, Vector3 RoomCenter,
    string? LandmarkGuid = null, bool IsFerryman = false,
    string? FerrymanEntranceRoomId = null, int FerrymanEntranceDoorId = 0);

internal sealed class MirrorTravel
{
    private TravelDestination? pending;
    private Vector3? arrival;
    private bool returnToSave;
    private bool ownsForcePosition;

    internal static bool TryGetPlayer(out Entity player)
    {
        player = null!;
        return !ApplicationFlowManager.IsLoading
            && MainCharacterSingleton.TryGetEntity(out player) && player;
    }

    internal static bool WasVisited(string roomId)
    {
        var explorer = MainCharacterSingleton.GetComponent<ExplorerComponent>();
        return explorer && explorer.worldRoomExplorationStates.TryGetValue(roomId, out var state)
            && state != null && state.isSaveRoom
            && (state.usedRoomSavePoint || state.ExploredAtLeastOneChunk() || state.enteredZonesIds?.Count > 0);
    }

    private static bool WasRoomVisited(string roomId)
    {
        var explorer = MainCharacterSingleton.GetComponent<ExplorerComponent>();
        return explorer && explorer.worldRoomExplorationStates.TryGetValue(roomId, out var state)
            && state != null
            && (state.usedRoomSavePoint || state.ExploredAtLeastOneChunk() || state.enteredZonesIds?.Count > 0);
    }

    private static bool WasLandmarkDiscovered(string guid)
    {
        var explorer = MainCharacterSingleton.GetComponent<ExplorerComponent>();
        if (!explorer || explorer.discoveredLandmarks == null) return false;
        foreach (var landmark in explorer.discoveredLandmarks)
            if (landmark && landmark.guid == guid) return true;
        return false;
    }

    internal static bool CanTravelToLandmark(LevelMapLandmark landmark)
    {
        return landmark && landmark.landmarkData && !string.IsNullOrEmpty(landmark.itemWorldRoomId)
            && WasRoomVisited(landmark.itemWorldRoomId)
            && WasLandmarkDiscovered(landmark.landmarkData.guid);
    }

    internal static bool CanTravelToFerryman(LevelMapZDoorItem door)
    {
        return door && door.isFastTravelDoor && !string.IsNullOrEmpty(door.linkedWorldRoomUniqueID)
            && WasFerrymanVisited(door.linkedWorldRoomUniqueID, door.itemWorldRoomId, door.zDoorID);
    }

    private static bool WasFerrymanVisited(string roomId, string? entranceRoomId, int entranceDoorId)
    {
        if (WasRoomVisited(roomId)) return true;
        var explorer = MainCharacterSingleton.GetComponent<ExplorerComponent>();
        // Special rooms may not have ordinary chunk exploration. Entering their
        // door is also a saved record that the destination has been visited.
        return explorer && !string.IsNullOrEmpty(entranceRoomId)
            && explorer.EnteredZDoor(entranceRoomId, entranceDoorId);
    }

    internal TravelDestination? FindDestination(LevelMapItem? item)
    {
        if (!item || pending != null || returnToSave || !TryGetPlayer(out _)) return null;
        var door = item!.TryCast<LevelMapZDoorItem>();
        var landmark = item.TryCast<LevelMapLandmark>();
        if (!door && !item.TryCast<LevelMapSavePointItem>() && !landmark) return null;
        var roomId = door ? door!.linkedWorldRoomUniqueID : item.itemWorldRoomId;
        if (string.IsNullOrEmpty(roomId)) return null;
        bool isLandmark = landmark;
        var isFerryman = door && CanTravelToFerryman(door!);
        if (isLandmark && !CanTravelToLandmark(landmark!)) return null;
        if (!isLandmark && !isFerryman && !WasVisited(roomId)) return null;
        var level = item.context?.level;
        var manager = LevelManager.Get();
        if (!level || !manager || !manager.worldDataDictionary.TryGetItem(level, out var reference)) return null;
        var world = ResourceManager.LoadAssetByRef<WorldData>(ResourceContext.UI_GAMEMENU, reference);
        if (!world) return null;
        foreach (var room in world.GetRooms())
        {
            if (room.id != roomId) continue;
            var configuration = room.roomData.configuration.TryCast<RedWineRoomConfiguration>();
            if (isFerryman && configuration?.isFastTravelRoom != true) return null;
            if (!isLandmark && !isFerryman && configuration?.isSaveRoom != true)
                return null;
            var label = isLandmark && landmark!.landmarkData.hasName
                ? landmark.landmarkData.landmarkName.GetLocalizedString() : null;
            if (string.IsNullOrWhiteSpace(label))
                label = GetRoomLabel(world, level!, room, isFerryman ? "Ferryman's Wharf" : "Mirror of Life");
            return new TravelDestination(roomId, level!.guid, label, RoomDesignerExtensions.GetRoomCenterPosition(room),
                isLandmark ? landmark!.landmarkData.guid : null, isFerryman,
                isFerryman ? door!.itemWorldRoomId : null, isFerryman ? door!.zDoorID : 0);
        }
        return null;
    }

    private static string GetRoomLabel(WorldData world, LevelID level, WorldRoomData room, string kind)
    {
        try
        {
            var district = world.configuration.TryCast<RedWineWorldConfiguration>()?.FindRoomDistrict(room.id);
            var area = district?.GetDistrictDisplayData()?.districtDisplayName?.GetLocalizedString();
            if (string.IsNullOrWhiteSpace(area))
                area = level.LoadLevelUIDisplayData()?.levelDisplayName?.GetLocalizedString();
            return string.IsNullOrWhiteSpace(area) ? kind : $"{kind} — {area}";
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Could not resolve the destination's area name: {e.Message}");
            return kind;
        }
    }

    private static LevelID? FindLevel(string guid)
    {
        var manager = LevelManager.Get();
        if (!manager) return null;
        foreach (var element in manager.applicationStateDictionary.elements)
            if (element.id && element.id.guid == guid) return element.id;
        return null;
    }

    internal void Start(TravelDestination destination)
    {
        if (pending != null || returnToSave || !TryGetPlayer(out var player) || !GameStateManager.GetInstance().IsGameplayState())
            throw new InvalidOperationException("Travel is only available during normal gameplay.");
        var level = FindLevel(destination.LevelGuid);
        var discovered = destination.IsFerryman
            ? WasFerrymanVisited(destination.RoomId, destination.FerrymanEntranceRoomId, destination.FerrymanEntranceDoorId)
            : destination.LandmarkGuid == null ? WasVisited(destination.RoomId)
            : WasRoomVisited(destination.RoomId) && WasLandmarkDiscovered(destination.LandmarkGuid);
        if (!discovered || !level)
            throw new InvalidOperationException("This destination has not been visited in the current save.");

        SaveLogic.Get().SaveNoPosition(player, SaveOrigin.Flow);
        pending = destination;
        arrival = null;
        var spawn = MainCharacterSpawnLogic.Get();
        // This only selects the room to stream. Place the player at the destination
        // before OnFirstRoomLoaded starts the room's camera and gameplay.
        try
        {
            // Acquire ownership before calling native code so failure can also clean up.
            ownsForcePosition = true;
            spawn.SetForcePosition(destination.RoomCenter, null);
            Plugin.Logger.LogInfo($"Travel requested: destination={destination.RoomId}, level={destination.LevelGuid}, streamingPosition={destination.RoomCenter}");
            TravelDiagnostics.Snapshot("travel-request");
            if (!LevelManager.Get().TryLoadLevelState(level, MainGameLoadingType.FastTravel, null))
                throw new InvalidOperationException("The game refused the level transition.");
            Plugin.Logger.LogInfo($"Travelling to {destination.Label}: room={destination.RoomId}, landmark={destination.LandmarkGuid}, ferryman={destination.IsFerryman}");
        }
        catch
        {
            pending = null;
            arrival = null;
            ReleaseForcePosition("transition refused or threw");
            throw;
        }
    }

    internal void PrepareArrival(MainCharacterSpawnLogic spawn)
    {
        if (pending == null) return;
        var level = LevelManager.Get().loadingLevelVariable.CurrentValue;
        if (!level || level.guid != pending.LevelGuid)
        {
            Plugin.Logger.LogWarning($"Travel interrupted by a different load: expectedLevel={pending.LevelGuid}, actualLevel={level?.guid}");
            pending = null;
            arrival = null;
            ReleaseForcePosition("different level initialized");
            return;
        }
        spawn.SetForcePosition(pending.RoomCenter, null);
    }

    internal void PlaceAtDestination(RoomManagerSystem manager, RoomStreamingHandle handle, ref RoomChunkID chunk)
    {
        if (pending == null) return;
        if (handle.worldRoomData.id != pending.RoomId || !handle.roomInstance)
            throw new InvalidOperationException("The loaded room does not match the selected destination.");
        var player = MainCharacterSingleton.GetEntity();
        Vector3 position;
        if (pending.IsFerryman)
        {
            position = FindFerrymanArrival(handle, player);
        }
        else if (pending.LandmarkGuid == null)
        {
            var mirrors = handle.roomInstance.GetComponentsInChildren<SavePoint>(true);
            if (mirrors.Length != 1)
                throw new InvalidOperationException($"Expected one mirror in {pending.RoomId}, found {mirrors.Length}.");
            position = mirrors[0].transform.position;
            // SavePoint's root is at the player's feet; artwork is offset into depth.
            position.z = player.transform.position.z;
            position.y += 0.01f;
        }
        else
        {
            position = FindLandmarkArrival(handle, player, pending.LandmarkGuid);
        }
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
            throw new InvalidOperationException("The destination has an invalid position.");
        MainCharacterSpawnLogic.Get().PlaceMainCharacter(position, player.transform.rotation);
        if (!manager.GetEntityRoomAndChunk(player.gameObject, out var room, out var mirrorChunk, out _)
            || room.worldRoomData.id != pending.RoomId)
            throw new InvalidOperationException("The arrival position is outside the destination room.");
        chunk = mirrorChunk;
        arrival = position;
        Plugin.Logger.LogInfo($"Placed player at {pending.RoomId}: {position}");
    }

    private static Vector3 FindLandmarkArrival(RoomStreamingHandle handle, Entity player, string landmarkGuid)
    {
        foreach (var landmark in handle.roomInstance.GetComponentsInChildren<LandmarkComponent>(true))
        {
            if (!landmark.landmarkData || landmark.landmarkData.guid != landmarkGuid) continue;
            if (TryFindStandingPosition(player, landmark.transform.position, out var position)) return position;
        }
        throw new InvalidOperationException($"No clear standing position found near landmark {landmarkGuid} in {handle.worldRoomData.id}.");
    }

    private static Vector3 FindFerrymanArrival(RoomStreamingHandle handle, Entity player)
    {
        if (handle.worldRoomData.roomData.configuration.TryCast<RedWineRoomConfiguration>()?.isFastTravelRoom != true)
            throw new InvalidOperationException("The selected room is not a ferryman room.");
        foreach (var ferryman in handle.roomInstance.GetComponentsInChildren<GPE_FastTravelComponent>(true))
            if (TryFindStandingPosition(player, ferryman.transform.position, out var position)) return position;
        throw new InvalidOperationException($"No clear standing position found near the ferryman in {handle.worldRoomData.id}.");
    }

    private static bool TryFindStandingPosition(Entity player, Vector3 anchor, out Vector3 position)
    {
        var motor = MainCharacterSingleton.GetComponent<CharacterMotor>();
        if (!motor) throw new InvalidOperationException("The player's character motor is unavailable.");
        Physics.SyncTransforms();
        // Destination anchors can be above the floor. Let the game's capsule
        // grounding code find a nearby standing position, then check clearance.
        anchor.z = player.transform.position.z;
        foreach (var offset in new[] { 0f, -1f, 1f, -2f, 2f, -3f, 3f })
        {
            position = anchor + new Vector3(offset, 2f, 0f);
            var ground = new CharacterGroundingReport();
            motor.ProbeGround(ref position, player.transform.rotation, 12f, ref ground);
            if (!ground.isStableOnGround || !ground.groundCollider) continue;
            position.y += 0.05f;
            if (motor.CharacterCollisionsOverlap(position, player.transform.rotation, new Collider[16], 0f, false) == 0)
                return true;
        }
        position = default;
        return false;
    }

    internal void FailArrival(Exception error)
    {
        Plugin.Logger.LogError($"Travel failed; returning to the saved mirror: {error}");
        pending = null;
        arrival = null;
        ReleaseForcePosition("arrival failed");
        returnToSave = true;
        TravelDiagnostics.Snapshot("arrival-failed");
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
        ReleaseForcePosition("arrival completed");
        pending = null;
        arrival = null;
        Plugin.Logger.LogInfo($"Arrived at {destination.Label}: room={destination.RoomId}, landmark={destination.LandmarkGuid}; player position verified (rendering is not verified)");
    }

    private void ReleaseForcePosition(string reason)
    {
        // Never clear spawn state for ordinary game transitions that we don't own.
        if (!ownsForcePosition) return;
        var spawn = MainCharacterSpawnLogic.Get();
        var wasForced = spawn._forceSpawnPosition;
        spawn.ClearForcePosition();
        ownsForcePosition = false;
        Plugin.Logger.LogInfo($"Released travel spawn override: reason={reason}, wasForced={wasForced}, forcedNow={spawn._forceSpawnPosition}");
    }

    internal void Update()
    {
        if (!returnToSave || ApplicationFlowManager.IsLoading) return;
        returnToSave = false;
        TravelDiagnostics.Snapshot("return-to-saved-mirror");
        LevelManager.Get().ReloadSaveState(MainGameLoadingType.FastTravel);
    }
}
