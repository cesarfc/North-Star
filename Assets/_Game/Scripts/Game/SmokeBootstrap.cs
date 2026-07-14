using UnityEngine;

/// <summary>
/// Drops the game into the Exploring state on play so the PlayerController (which freezes
/// movement outside Exploring) is active immediately, whether the scene was entered from the
/// boot menu or opened directly. Optionally announces the starting zone on the EventBus so
/// zone-reactive systems (music playlists, world map discovery) initialize without needing a
/// gate transition first.
/// </summary>
public class SmokeBootstrap : MonoBehaviour
{
    [Tooltip("When set, published as ZoneEnteredEvent on play (drives zone music/map). " +
             "Empty = no announcement (smoke scene).")]
    [SerializeField] private string _initialZoneId = "";

    private void Start()
    {
        GameManager.Instance?.ChangeState(GameState.Exploring);

        if (!string.IsNullOrEmpty(_initialZoneId))
            EventBus.Publish(new ZoneEnteredEvent { zoneId = _initialZoneId, fromZoneId = null });
    }
}
