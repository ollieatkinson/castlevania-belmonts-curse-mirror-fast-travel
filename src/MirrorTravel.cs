using System.Text.Json;
using System.Text.Json.Serialization;
using BepInEx;
using MotherBase.ApplicationFlowManager;
using MotherBase.EntityFramework;
using UnityEngine;

namespace MirrorFastTravel;

internal sealed record Mirror(string RoomId, string LevelGuid, string Label, float X, float Y, float Z)
{
    [JsonIgnore]
    public Vector3 Position => new(X, Y, Z);
}

internal sealed class MirrorTravel
{
    private readonly string path = Path.Combine(Paths.ConfigPath, Plugin.Id + ".mirrors.json");
    private readonly List<Mirror> mirrors;
    private Mirror? pending;

    internal MirrorTravel()
    {
        try
        {
            mirrors = File.Exists(path)
                ? JsonSerializer.Deserialize<List<Mirror>>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch (Exception e)
        {
            Plugin.Logger.LogWarning($"Could not read mirror records: {e.Message}");
            mirrors = new();
        }
    }

    internal bool IsTravelling => pending != null;

    internal static bool NearMirror(out Entity player)
    {
        player = null!;
        if (ApplicationFlowManager.IsLoading || !MainCharacterSingleton.TryGetEntity(out player) || !player)
            return false;
        var room = LevelHelpers.GetCurrentRoom(player);
        if (!room || room.worldRoomData?.roomData?.configuration?.TryCast<RedWineRoomConfiguration>()?.isSaveRoom != true)
            return false;
        foreach (var mirror in UnityEngine.Object.FindObjectsOfType<SavePoint>())
        {
            if (mirror.isActiveAndEnabled && Vector2.Distance(player.transform.position, mirror.transform.position) < 6f)
                return true;
        }
        return false;
    }

    internal void Record(Entity player, Vector3 position)
    {
        var room = LevelHelpers.GetCurrentRoom(player)?.worldRoomData;
        var level = LevelManager.Get()?.currentLevelVariable?.CurrentValue;
        if (room?.roomData?.configuration?.TryCast<RedWineRoomConfiguration>()?.isSaveRoom != true || !level)
            return;
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) return;

        var entry = new Mirror(room.id, level!.guid, $"{level.name} — {room.instanceName}", position.x, position.y, position.z);
        mirrors.RemoveAll(m => m.RoomId == entry.RoomId);
        mirrors.Add(entry);
        Directory.CreateDirectory(Paths.ConfigPath);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(mirrors));
        File.Move(path + ".tmp", path, overwrite: true);
        Plugin.Logger.LogInfo($"Recorded mirror {entry.Label} at {position}");
    }

    internal List<Mirror> GetDestinations()
    {
        var explorer = MainCharacterSingleton.GetComponent<ExplorerComponent>();
        var origin = LevelHelpers.GetCurrentRoom(MainCharacterSingleton.GetEntity())?.worldRoomData?.id;
        if (!explorer) return new();
        return mirrors.Where(m => m.RoomId != origin && explorer.UsedSavePoint(m.RoomId) && FindLevel(m.LevelGuid))
            .OrderBy(m => m.Label).ToList();
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
        if (pending != null || !NearMirror(out var player) || !GameStateManager.GetInstance().IsGameplayState())
            throw new InvalidOperationException("Travel is only available beside a mirror during normal gameplay.");
        var explorer = MainCharacterSingleton.GetComponent<ExplorerComponent>();
        var level = FindLevel(destination.LevelGuid);
        if (!explorer || !explorer.UsedSavePoint(destination.RoomId) || !level)
            throw new InvalidOperationException("This mirror is not available in the current save.");

        // Match normal fast travel's save-before-load sequence. Do not change
        // exploration flags, unlock destinations, or overwrite the respawn point.
        SaveLogic.Get().SaveNoPosition(player, SaveOrigin.Flow);
        pending = destination;
        var spawn = MainCharacterSpawnLogic.Get();
        spawn.SetForcePosition(destination.Position, null);
        try
        {
            if (!LevelManager.Get().TryLoadLevelState(level, MainGameLoadingType.FastTravel, null))
                throw new InvalidOperationException("The game refused the level transition.");
            Plugin.Logger.LogInfo($"Travelling to {destination.Label} at {destination.Position}");
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
        spawn.SetForcePosition(pending.Position, null);
    }

    internal void CheckArrival()
    {
        if (pending == null) return;
        var destination = pending;
        pending = null;
        var player = MainCharacterSingleton.GetEntity();
        var roomId = LevelHelpers.GetCurrentRoom(player)?.worldRoomData?.id;
        var distance = Vector3.Distance(player.transform.position, destination.Position);
        if (roomId == destination.RoomId && distance < 3f)
            Plugin.Logger.LogInfo($"Arrived at {destination.Label} (distance {distance:F2})");
        else
            Plugin.Logger.LogWarning($"Mirror arrival needs checking: expected {destination.RoomId}, got {roomId}; distance {distance:F2}");
    }
}
