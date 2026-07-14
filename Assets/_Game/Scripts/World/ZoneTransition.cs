using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trigger volume that moves the player to another zone by loading its scene
/// <b>additively</b> (never <see cref="LoadSceneMode.Single"/>) via
/// <see cref="SceneManager.LoadSceneAsync(string, LoadSceneMode)"/>. When the player enters
/// the volume it publishes <see cref="ZoneEnteredEvent"/> and, the first time a destination
/// is reached, <see cref="ZoneDiscoveredEvent"/> on the <see cref="EventBus"/> so the world
/// map and other systems react without any direct cross-module reference.
///
/// Configure the destination by assigning <see cref="_targetZone"/> (provides the scene name,
/// zone id, and discovery flag) and a <see cref="_spawnPointId"/> naming the
/// <c>SpawnPoint_[id]</c> GameObject to place the player at in the loaded scene. The trigger
/// only reacts to colliders tagged <see cref="_playerTag"/>.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ZoneTransition : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Zone this volume travels to. Supplies sceneId, zoneId and the discovery flag.")]
    [SerializeField] private WorldZoneData _targetZone;

    [Tooltip("Names the SpawnPoint_[id] GameObject the player is placed at in the loaded scene.")]
    [SerializeField] private string _spawnPointId;

    [Tooltip("Zone the player is leaving (for ZoneEnteredEvent.fromZoneId). Optional.")]
    [SerializeField] private string _fromZoneId;

    [Header("Trigger")]
    [Tooltip("Only colliders with this tag trigger the transition.")]
    [SerializeField] private string _playerTag = "Player";

    private bool _isTransitioning;

    // The collider that tripped the trigger — placed at the spawn point once the load completes.
    private Collider _traveler;

    /// <summary>The spawn-point id this transition targets in the destination scene.</summary>
    public string SpawnPointId => _spawnPointId;

    // ── INTERFACE.md events ───────────────────────────────────────────────────

    /// <summary>Raised when a transition begins, carrying (fromScene, toScene) scene names.</summary>
    public event Action<string, string> OnTransitionStart;

    /// <summary>Raised when the destination scene has finished loading additively.</summary>
    public event Action<string> OnTransitionComplete;

    private void Reset()
    {
        // Trigger volumes must be triggers.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isTransitioning) return;
        if (!other.CompareTag(_playerTag)) return;
        if (_targetZone == null)
        {
            Debug.LogWarning($"[ZoneTransition] '{name}' has no target zone assigned.");
            return;
        }

        _traveler = other;
        TravelTo(_targetZone.sceneId, _spawnPointId);
    }

    /// <summary>
    /// Travel to the scene named <paramref name="sceneId"/>, placing the player at the
    /// <c>SpawnPoint_[spawnPointId]</c> GameObject. Loads the scene additively and, once it
    /// is ready, publishes the world events and fires <see cref="OnTransitionComplete"/>.
    /// No-op while a transition is already in flight or if the scene name is empty.
    /// </summary>
    public void TravelTo(string sceneId, string spawnPointId)
    {
        if (_isTransitioning) return;
        if (string.IsNullOrEmpty(sceneId))
        {
            Debug.LogWarning($"[ZoneTransition] '{name}' asked to travel to an empty sceneId.");
            return;
        }

        StartCoroutine(CoTravel(sceneId, spawnPointId));
    }

    /// <summary>
    /// Coroutine driving the additive load: announce start, await the async load, publish
    /// the world events, then announce completion.
    /// </summary>
    private IEnumerator CoTravel(string sceneId, string spawnPointId)
    {
        _isTransitioning = true;

        string fromScene = SceneManager.GetActiveScene().name;
        OnTransitionStart?.Invoke(fromScene, sceneId);

        // Additive only — zone scenes layer on top of the persistent scene; never Single.
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneId, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"[ZoneTransition] Failed to start additive load of scene '{sceneId}'. " +
                           "Is it in Build Settings?");
            _isTransitioning = false;
            yield break;
        }

        while (!op.isDone)
            yield return null;

        PlaceTraveler(sceneId, spawnPointId);
        PublishArrival(sceneId, spawnPointId);

        OnTransitionComplete?.Invoke(sceneId);
        _isTransitioning = false;
    }

    /// <summary>
    /// Move the traveler that tripped this trigger to <c>SpawnPoint_[spawnPointId]</c> in the
    /// freshly loaded scene, fulfilling the placement contract documented on this class. A
    /// <see cref="CharacterController"/> is toggled off around the move so the controller
    /// doesn't overwrite the teleport on its next internal Move. No-op when the spawn id is
    /// empty or the point doesn't exist (a warning is logged so authors catch the typo).
    /// </summary>
    private void PlaceTraveler(string sceneId, string spawnPointId)
    {
        if (_traveler == null || string.IsNullOrEmpty(spawnPointId)) return;

        Transform spawn = FindSpawnPoint(SceneManager.GetSceneByName(sceneId), spawnPointId);
        if (spawn == null)
        {
            Debug.LogWarning($"[ZoneTransition] SpawnPoint_{spawnPointId} not found in scene '{sceneId}' — " +
                             "traveler left in place.");
            return;
        }

        var controller = _traveler as CharacterController;
        if (controller != null) controller.enabled = false;
        _traveler.transform.SetPositionAndRotation(
            spawn.position,
            Quaternion.Euler(0f, spawn.eulerAngles.y, 0f));
        if (controller != null) controller.enabled = true;
        _traveler = null;
    }

    /// <summary>
    /// Find the <c>SpawnPoint_[spawnPointId]</c> transform anywhere in <paramref name="scene"/>,
    /// or <c>null</c> when the scene isn't loaded or has no such object. Static so the lookup
    /// convention is unit-testable without a transition volume.
    /// </summary>
    public static Transform FindSpawnPoint(Scene scene, string spawnPointId)
    {
        if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(spawnPointId)) return null;

        string target = "SpawnPoint_" + spawnPointId;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == target) return root.transform;
            Transform found = FindChildDeep(root.transform, target);
            if (found != null) return found;
        }
        return null;
    }

    private static Transform FindChildDeep(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName) return child;
            Transform found = FindChildDeep(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// Publish <see cref="ZoneEnteredEvent"/> always and <see cref="ZoneDiscoveredEvent"/>
    /// when the destination zone is not discovered-by-default. The WorldMapManager listens
    /// for ZoneEnteredEvent and performs idempotent discovery, so re-entering a known zone
    /// is harmless.
    /// </summary>
    private void PublishArrival(string sceneId, string spawnPointId)
    {
        string zoneId = _targetZone != null ? _targetZone.zoneId : sceneId;

        EventBus.Publish(new ZoneEnteredEvent { zoneId = zoneId, fromZoneId = _fromZoneId });

        if (_targetZone == null || !_targetZone.isDiscoveredByDefault)
            EventBus.Publish(new ZoneDiscoveredEvent { zoneId = zoneId });
    }
}
