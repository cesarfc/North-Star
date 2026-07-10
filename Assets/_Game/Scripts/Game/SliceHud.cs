using System.Text;
using NorthStar.Inventory;
using NorthStar.Player;
using UnityEngine;
// DayNightCycle (Landscape module) is in the global namespace — no using needed.

/// <summary>
/// Minimal IMGUI heads-up display for the vertical slice. Reads live state from the
/// PlayerStats, Inventory and DayNightCycle systems each frame to show they're wired
/// and running together. Composition-root glue (NorthStar.Game).
/// </summary>
public class SliceHud : MonoBehaviour
{
    [SerializeField] private PlayerStats _stats;
    [SerializeField] private Inventory _inventory;
    [SerializeField] private DayNightCycle _dayNight;
    [SerializeField] private QuestManager _quests; // optional — quest log lines when wired

    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.box)
        {
            fontSize = 15,
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(10, 10, 8, 8),
        };

        var sb = new StringBuilder();
        if (_dayNight != null) sb.AppendLine($"Time   {FormatHour(_dayNight.GetCurrentHour())}");
        if (_stats != null) sb.AppendLine($"HP     {_stats.CurrentHP}/{_stats.MaxHP}     Gold {_stats.Gold}");
        if (_inventory != null) sb.Append($"Items  {TotalItems(_inventory)}");

        GUI.Box(new Rect(10, 36, 300, 90), sb.ToString(), style);

        if (_quests == null) return;
        QuestData[] active = _quests.GetActiveQuests();
        if (active == null || active.Length == 0) return;
        var quests = new StringBuilder("QUESTS");
        foreach (QuestData quest in active)
            if (quest != null) quests.Append($"\n• {quest.displayName}");
        GUI.Box(new Rect(10, 132, 300, 30f + active.Length * 20f), quests.ToString(), style);
    }

    private static string FormatHour(float hour)
    {
        int h = Mathf.FloorToInt(hour) % 24;
        int m = Mathf.FloorToInt((hour - Mathf.Floor(hour)) * 60f);
        return $"{h:00}:{m:00}";
    }

    private static int TotalItems(Inventory inv)
    {
        int total = 0;
        foreach (var entry in inv.GetAllItems()) total += entry.quantity;
        return total;
    }
}
