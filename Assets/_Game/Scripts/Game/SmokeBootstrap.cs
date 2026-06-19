using UnityEngine;

/// <summary>
/// Drops the game into the Exploring state on play so the PlayerController (which freezes
/// movement outside Exploring) is active immediately in the smoke scene. Stand-in for a real
/// main-menu → gameplay flow.
/// </summary>
public class SmokeBootstrap : MonoBehaviour
{
    private void Start()
    {
        GameManager.Instance?.ChangeState(GameState.Exploring);
    }
}
