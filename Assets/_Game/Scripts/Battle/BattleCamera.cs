using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Dynamic combat framing built on Cinemachine 3.x. Activates a dedicated battle
/// <see cref="CinemachineCamera"/> when a battle starts and frames the action by pointing the
/// rig's Follow/LookAt at a midpoint anchor between the two sides; deactivates on battle end.
/// All wiring is driven by EventBus battle events so the camera holds no reference to the
/// Battle gameplay code.
/// </summary>
public class BattleCamera : MonoBehaviour
{
    [Header("Cinemachine (3.x)")]
    [Tooltip("Camera that becomes live during combat. Raise its priority above the exploration rig.")]
    [SerializeField] private CinemachineCamera _battleCamera;

    [Header("Priorities")]
    [SerializeField] private int _activePriority = 30;
    [SerializeField] private int _inactivePriority = 0;

    [Header("Framing")]
    [Tooltip("Anchor the battle camera follows; positioned at the midpoint of all combatants. " +
             "Created at runtime if left unassigned.")]
    [SerializeField] private Transform _framingAnchor;

    private ICombatant[] _allies = System.Array.Empty<ICombatant>();
    private ICombatant[] _enemies = System.Array.Empty<ICombatant>();
    private bool _active;

    private void Awake()
    {
        if (_framingAnchor == null)
        {
            var go = new GameObject("BattleCameraFramingAnchor");
            go.transform.SetParent(transform, false);
            _framingAnchor = go.transform;
        }
        SetActiveState(false);
    }

    private void OnEnable()
    {
        EventBus.Subscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Subscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Subscribe<UnitDiedEvent>(OnUnitDied);
    }

    private void OnDisable()
    {
        EventBus.Unsubscribe<BattleStartedEvent>(OnBattleStarted);
        EventBus.Unsubscribe<BattleEndedEvent>(OnBattleEnded);
        EventBus.Unsubscribe<UnitDiedEvent>(OnUnitDied);
    }

    private void OnBattleStarted(BattleStartedEvent e)
    {
        _allies  = e.allies  ?? System.Array.Empty<ICombatant>();
        _enemies = e.enemies ?? System.Array.Empty<ICombatant>();
        SetActiveState(true);
        UpdateFraming();
    }

    private void OnBattleEnded(BattleEndedEvent e)
    {
        SetActiveState(false);
        _allies  = System.Array.Empty<ICombatant>();
        _enemies = System.Array.Empty<ICombatant>();
    }

    private void OnUnitDied(UnitDiedEvent e)
    {
        // Re-tighten framing on the survivors when a combatant falls.
        if (_active) UpdateFraming();
    }

    /// <summary>
    /// Re-frame the battle by moving the framing anchor to the average position of all living
    /// combatants and pointing the battle rig at it. Call when the roster changes.
    /// </summary>
    public void UpdateFraming()
    {
        if (_framingAnchor == null) return;

        Vector3 sum = Vector3.zero;
        int count = 0;
        AccumulateLiving(_allies, ref sum, ref count);
        AccumulateLiving(_enemies, ref sum, ref count);

        if (count > 0)
            _framingAnchor.position = sum / count;

        if (_battleCamera != null)
        {
            _battleCamera.Follow = _framingAnchor;
            _battleCamera.LookAt = _framingAnchor;
        }
    }

    private static void AccumulateLiving(ICombatant[] units, ref Vector3 sum, ref int count)
    {
        foreach (var u in units)
        {
            if (u == null || !u.IsAlive || u.Anchor == null) continue;
            sum += u.Anchor.position;
            count++;
        }
    }

    private void SetActiveState(bool active)
    {
        _active = active;
        if (_battleCamera != null)
            _battleCamera.Priority = active ? _activePriority : _inactivePriority;
    }
}
