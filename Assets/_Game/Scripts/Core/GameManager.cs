using System;
using UnityEngine;

/// <summary>
/// Global singleton managing game state transitions.
/// Prefer subscribing to <see cref="GameStateChangedEvent"/> via EventBus for
/// cross-module decoupling; the C# <see cref="OnStateChanged"/> event is provided
/// for tightly-coupled local listeners that already hold a GameManager reference.
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>The active GameManager instance, set in Awake and persisted across scenes.</summary>
    public static GameManager Instance { get; private set; }

    /// <summary>The current game state. Mutated only via <see cref="ChangeState"/>.</summary>
    public GameState CurrentState { get; private set; } = GameState.MainMenu;

    /// <summary>Raised after the state changes. Arguments are (previous, next).</summary>
    public event Action<GameState, GameState> OnStateChanged;

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

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Transition to a new game state. No-op if already in that state. Fires the
    /// <see cref="OnStateChanged"/> C# event and publishes <see cref="GameStateChangedEvent"/>
    /// on the EventBus.
    /// </summary>
    /// <param name="newState">The state to transition into.</param>
    public void ChangeState(GameState newState)
    {
        if (newState == CurrentState) return;

        var prev = CurrentState;
        CurrentState = newState;

        OnStateChanged?.Invoke(prev, newState);

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
