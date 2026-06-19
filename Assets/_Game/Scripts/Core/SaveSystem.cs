using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Handles reading and writing GameSaveData to disk as JSON.
/// Save files are stored in Application.persistentDataPath/saves/.
/// </summary>
public static class SaveSystem
{
    private static string SaveDir => Path.Combine(Application.persistentDataPath, "saves");

    private static string SlotPath(string slotName) =>
        Path.Combine(SaveDir, $"{slotName}.json");

    /// <summary>
    /// Serialize and write a save file as JSON. Stamps <see cref="GameSaveData.savedAt"/>
    /// with the current UTC time (ISO-8601). Returns false on null data, an empty slot
    /// name, or any I/O failure.
    /// </summary>
    /// <param name="slotName">Slot identifier; becomes the file name.</param>
    /// <param name="data">The save payload. Must not be null.</param>
    public static bool Save(string slotName, GameSaveData data)
    {
        if (string.IsNullOrEmpty(slotName))
        {
            Debug.LogError("[SaveSystem] Save called with an empty slot name.");
            return false;
        }
        if (data == null)
        {
            Debug.LogError($"[SaveSystem] Save called with null data for '{slotName}'.");
            return false;
        }

        try
        {
            Directory.CreateDirectory(SaveDir);
            data.savedAt = DateTime.UtcNow.ToString("o");
            File.WriteAllText(SlotPath(slotName), JsonUtility.ToJson(data, prettyPrint: true));
            Debug.Log($"[SaveSystem] Saved slot '{slotName}'");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to save '{slotName}': {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Load and deserialize a save file. Returns null if the slot name is empty,
    /// the file does not exist, or deserialization fails. Callers must null-check.
    /// </summary>
    /// <param name="slotName">Slot identifier to load.</param>
    public static GameSaveData Load(string slotName)
    {
        if (string.IsNullOrEmpty(slotName)) return null;

        var path = SlotPath(slotName);
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[SaveSystem] Save '{slotName}' not found.");
            return null;
        }

        try
        {
            return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Failed to load '{slotName}': {e.Message}");
            return null;
        }
    }

    /// <summary>Delete a save slot from disk. No-op if the slot does not exist.</summary>
    /// <param name="slotName">Slot identifier to delete.</param>
    public static void DeleteSave(string slotName)
    {
        if (string.IsNullOrEmpty(slotName)) return;
        var path = SlotPath(slotName);
        if (File.Exists(path)) File.Delete(path);
    }

    /// <summary>Returns true if a save file exists for the given slot.</summary>
    /// <param name="slotName">Slot identifier to test.</param>
    public static bool SaveExists(string slotName) =>
        !string.IsNullOrEmpty(slotName) && File.Exists(SlotPath(slotName));
}

// ─────────────────────────────────────────────
// DATA SHAPE — add fields, never remove/rename
// ─────────────────────────────────────────────

[Serializable]
public class GameSaveData
{
    public string savedAt;
    public float playTimeSeconds;
    public string currentZoneId;
    public string activeQuestId;
    public CharacterLoadout loadout;
    public InventorySnapshot inventory;
    // Quest flags: key = questId, value = completed
    public QuestFlagEntry[] questFlags = Array.Empty<QuestFlagEntry>();
}

[Serializable]
public struct QuestFlagEntry
{
    public string questId;
    public bool completed;
}
