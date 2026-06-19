using UnityEngine;

/// <summary>
/// Global singleton managing game state transitions.
/// Subscribe to OnStateChanged via EventBus, not directly.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Transition to a new game state. Fires GameStateChangedEvent via EventBus.
    /// </summary>
    public void ChangeState(GameState newState)
    {
        if (newState == CurrentState) return;

        var prev = CurrentState;
        CurrentState = newState;

        EventBus.Publish(new GameStateChangedEvent
        {
            prev = prev,
            next = newState
        });

        Debug.Log($"[GameManager] State: {prev} → {newState}");
    }
}

public enum GameState
{
    MainMenu,
    Exploring,
    Battle,
    Cutscene,
    Paused,
    GameOver
}
