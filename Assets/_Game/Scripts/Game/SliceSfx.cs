using NorthStar.Audio;
using UnityEngine;
// ItemAddedEvent and BattleStartedEvent (Core module) are in the global namespace — no using needed.

/// <summary>
/// Plays one-shot SFX in the vertical slice in response to gameplay events on the EventBus:
/// an item entering the Inventory (<see cref="ItemAddedEvent"/>) and a battle starting
/// (<see cref="BattleStartedEvent"/>). Composition-root glue (NorthStar.Game) — the only place
/// allowed to hold a direct <see cref="AudioManager"/> reference. Subscribes in OnEnable and
/// unsubscribes in OnDisable. clipIds are placeholders; the wiring stays silent until a matching
/// clip set is registered on the AudioManager.
/// </summary>
public class SliceSfx : MonoBehaviour
{
    private const string PICKUP_SFX_ID = "sfx-pickup";
    private const string BATTLE_SFX_ID = "sfx-battle";

    [Header("References")]
    [Tooltip("AudioManager that resolves and plays the SFX clipIds.")]
    [SerializeField] private AudioManager _audioManager;

    private void OnEnable()
    {
        EventBus.Subscribe<ItemAddedEvent>(OnItemAdded);
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<ItemAddedEvent>(OnItemAdded);
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
    }

    /// <summary>Play the pickup SFX when an item is added to the inventory.</summary>
    private void OnItemAdded(ItemAddedEvent e)
    {
        if (_audioManager == null) return;
        _audioManager.PlaySFX(PICKUP_SFX_ID);
    }

    /// <summary>Play the battle-start SFX when a battle begins.</summary>
    private void OnBattleStarted(BattleStartedEvent e)
    {
        if (_audioManager == null) return;
        _audioManager.PlaySFX(BATTLE_SFX_ID);
    }
}
