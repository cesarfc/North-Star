using UnityEngine;

/// <summary>
/// Implement this on any object the player can interact with:
/// NPCs, doors, chests, pickups, signs, levers, etc.
/// </summary>
public interface IInteractable
{
    /// <summary>Text shown in the interaction prompt UI (e.g. "Talk", "Open", "Pick up").</summary>
    string InteractionPrompt { get; }

    /// <summary>
    /// Called when the player presses the interact button while this object is in range.
    /// </summary>
    /// <param name="interactor">The player GameObject initiating the interaction.</param>
    void Interact(GameObject interactor);
}
