using UnityEngine;

/// <summary>
/// An interactable NPC that starts a real Dialogue-module conversation: interacting calls
/// <see cref="DialogueSystem.StartDialogue"/> with the configured Yarn graph id, which flips
/// the game into Cutscene state and streams lines/choices to the on-screen dialogue UI.
/// Replaces the hard-coded SmokeNPC in the vertical slice. Composition-root glue (NorthStar.Game).
/// </summary>
public class DialogueNPC : MonoBehaviour, IInteractable
{
    [Tooltip("The scene's DialogueSystem (Dialogue module).")]
    [SerializeField] private DialogueSystem _dialogue;

    [Tooltip("Yarn node to start when the player talks to this NPC.")]
    [SerializeField] private string _graphId = "ElderVane_Intro";

    [Tooltip("Name shown in the interaction prompt.")]
    [SerializeField] private string _npcName = "Elder Vane";

    /// <inheritdoc />
    public string InteractionPrompt => $"Talk to {_npcName} (E)";

    /// <inheritdoc />
    public void Interact(GameObject interactor)
    {
        if (_dialogue == null)
        {
            Debug.LogWarning($"[DialogueNPC] '{name}' has no DialogueSystem wired.", this);
            return;
        }
        if (_dialogue.IsDialogueActive) return;
        _dialogue.StartDialogue(_graphId);
    }
}
