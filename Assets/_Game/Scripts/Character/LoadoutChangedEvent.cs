namespace NorthStar.Character
{
    /// <summary>
    /// Published on the <see cref="EventBus"/> whenever a character's visual loadout
    /// changes (armor equipped/removed, hairstyle or hair color set). Carries the full,
    /// save-safe <see cref="CharacterLoadout"/> so subscribers in other modules can react
    /// without referencing the Character assembly.
    /// </summary>
    /// <remarks>
    /// Ideally this struct lives in <c>Core/GameEvents.cs</c> alongside the other canonical
    /// events so any module can subscribe to it. INTERFACE.md does not yet define a character
    /// event, and Core is read-only for this module, so it is declared here for now. The
    /// orchestrator should promote it into <c>GameEvents.cs</c> when the contract is updated.
    /// </remarks>
    public struct LoadoutChangedEvent
    {
        public CharacterLoadout loadout;
    }
}
