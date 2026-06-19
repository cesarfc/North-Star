using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NorthStar.Player
{
    /// <summary>
    /// Z-targeting / lock-on. On the lock-on action it runs a
    /// <c>Physics.OverlapSphere</c> over the enemy layer, picks the nearest
    /// candidate inside battle range that is roughly in front of the player, and
    /// hands it to the <see cref="CameraController"/> to frame. Pressing again
    /// while locked cycles to the next target; releasing or losing the target
    /// clears the lock. Targets are referenced purely by Transform/layer so this
    /// module stays decoupled from the Battle module's concrete types.
    /// </summary>
    public class LockOnSystem : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;
        [Tooltip("Map that owns the 'LockOn' action. Battle by default; Exploration also works.")]
        [SerializeField] private string _actionMap = "Battle";

        [Header("Detection")]
        [Tooltip("Maximum distance at which an enemy can be locked on.")]
        [SerializeField] private float _battleRange = 15f;
        [Tooltip("Physics layers that contain lockable enemies.")]
        [SerializeField] private LayerMask _enemyMask;
        [Tooltip("Only enemies within this half-angle (deg) of the camera/player forward are eligible. 180 = all around.")]
        [Range(1f, 180f)]
        [SerializeField] private float _acquisitionConeHalfAngle = 80f;
        [SerializeField] private int _maxColliders = 32;

        [Header("References")]
        [SerializeField] private CameraController _cameraController;
        [Tooltip("Forward reference for cone filtering. Defaults to this transform.")]
        [SerializeField] private Transform _aimReference;

        private const string LOCKON_ACTION = "LockOn";

        private InputAction _lockOnAction;
        private Collider[] _hits;
        private Transform _currentTarget;
        private bool _inputEnabled;

        /// <summary>The transform currently locked on to, or null.</summary>
        public Transform CurrentTarget => _currentTarget;

        /// <summary>True while a target is locked.</summary>
        public bool HasTarget => _currentTarget != null;

        /// <summary>Raised when a target is acquired.</summary>
        public event Action<Transform> OnTargetAcquired;

        /// <summary>Raised when the lock is dropped (manually or because the target was lost).</summary>
        public event Action OnTargetCleared;

        private void Awake()
        {
            _hits = new Collider[Mathf.Max(1, _maxColliders)];
            if (_aimReference == null) _aimReference = transform;
            CacheInputAction();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

            if (_lockOnAction != null)
            {
                _lockOnAction.performed += OnLockOnPerformed;
                _lockOnAction.Enable();
            }
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);

            if (_lockOnAction != null)
            {
                _lockOnAction.performed -= OnLockOnPerformed;
                _lockOnAction.Disable();
            }
            ClearTarget();
        }

        private void Update()
        {
            // Drop the lock if the target died/was destroyed or strayed out of range.
            if (_currentTarget == null) return;

            if (!_currentTarget.gameObject.activeInHierarchy ||
                (_currentTarget.position - _aimReference.position).sqrMagnitude > _battleRange * _battleRange)
            {
                ClearTarget();
            }
        }

        private void CacheInputAction()
        {
            if (_inputActions == null) return;
            var map = _inputActions.FindActionMap(_actionMap, throwIfNotFound: false);
            _lockOnAction = map?.FindAction(LOCKON_ACTION, throwIfNotFound: false);
            if (_lockOnAction == null)
                Debug.LogError($"[LockOnSystem] '{LOCKON_ACTION}' action not found on map '{_actionMap}'.");
        }

        private void OnLockOnPerformed(InputAction.CallbackContext _)
        {
            if (!_inputEnabled) return;
            ToggleLockOn();
        }

        /// <summary>Acquire the nearest target if none is held, cycle to the next if one is, drop if none remain.</summary>
        public void ToggleLockOn()
        {
            var candidates = GatherCandidates();
            if (candidates.Count == 0)
            {
                ClearTarget();
                return;
            }

            // Sort by squared distance so cycling goes nearest → farthest.
            candidates.Sort((a, b) =>
            {
                float da = (a.position - _aimReference.position).sqrMagnitude;
                float db = (b.position - _aimReference.position).sqrMagnitude;
                return da.CompareTo(db);
            });

            Transform next;
            if (_currentTarget == null)
            {
                next = candidates[0];
            }
            else
            {
                int idx = candidates.IndexOf(_currentTarget);
                next = idx >= 0 ? candidates[(idx + 1) % candidates.Count] : candidates[0];
                // Cycling back onto the only/last target releases the lock.
                if (candidates.Count == 1 && next == _currentTarget)
                {
                    ClearTarget();
                    return;
                }
            }

            SetTarget(next);
        }

        /// <summary>OverlapSphere scan returning all eligible, in-cone enemy transforms within range.</summary>
        private List<Transform> GatherCandidates()
        {
            var results = new List<Transform>();
            int count = Physics.OverlapSphereNonAlloc(
                _aimReference.position, _battleRange, _hits, _enemyMask, QueryTriggerInteraction.Collide);

            float cosHalf = Mathf.Cos(_acquisitionConeHalfAngle * Mathf.Deg2Rad);

            for (int i = 0; i < count; i++)
            {
                var col = _hits[i];
                if (col == null) continue;

                Transform t = col.transform;
                Vector3 toTarget = t.position - _aimReference.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f) continue;

                // In-cone test against the aim reference's forward (camera-aligned player).
                Vector3 fwd = _aimReference.forward;
                fwd.y = 0f;
                if (Vector3.Dot(fwd.normalized, toTarget.normalized) < cosHalf) continue;

                if (!results.Contains(t)) results.Add(t);
            }
            return results;
        }

        private void SetTarget(Transform target)
        {
            _currentTarget = target;
            _cameraController?.SetLockOnTarget(target);
            OnTargetAcquired?.Invoke(target);
        }

        /// <summary>Manually drop the current lock and restore free-look framing.</summary>
        public void ClearTarget()
        {
            if (_currentTarget == null) return;
            _currentTarget = null;
            _cameraController?.ClearLockOn();
            OnTargetCleared?.Invoke();
        }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            // Lock-on is meaningful in battle and exploration; suppress elsewhere.
            _inputEnabled = e.next == GameState.Battle || e.next == GameState.Exploring;
            if (!_inputEnabled) ClearTarget();
        }
    }
}
