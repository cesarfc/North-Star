using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NorthStar.Player
{
    /// <summary>
    /// Proximity-based interaction. Uses <c>Physics.OverlapSphere</c> (never a
    /// raycast) on a tunable cadence to find the nearest <see cref="IInteractable"/>
    /// in front of the player, raises enter/exit events so the UI can show a prompt,
    /// and fires <see cref="OnInteract"/> when the interact action (E / gamepad) is
    /// pressed. Interaction is suppressed outside the Exploring state.
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset _inputActions;
        [Tooltip("Action map that owns the 'Interact' action.")]
        [SerializeField] private string _actionMap = "Exploration";

        [Header("Detection")]
        [Tooltip("Radius of the overlap sphere used to find interactables.")]
        [SerializeField] private float _detectionRadius = 2.5f;
        [Tooltip("Layers that may contain interactables.")]
        [SerializeField] private LayerMask _interactableMask = ~0;
        [Tooltip("Local offset from the player root where the sphere is centered.")]
        [SerializeField] private Vector3 _detectionOffset = new Vector3(0f, 1f, 0.5f);
        [Tooltip("How many seconds between detection scans. 0 = every frame.")]
        [SerializeField] private float _scanInterval = 0.1f;
        [Tooltip("Maximum colliders considered per scan (non-allocating buffer size).")]
        [SerializeField] private int _maxColliders = 16;

        private const string INTERACT_ACTION = "Interact";

        private InputAction _interactAction;
        private Collider[] _hits;
        private IInteractable _current;
        private bool _enabled = true;
        private Coroutine _scanRoutine;

        /// <summary>Raised when an interactable becomes the closest in range.</summary>
        public event Action<IInteractable> OnInteractableEntered;

        /// <summary>Raised when the previously-closest interactable leaves range.</summary>
        public event Action<IInteractable> OnInteractableExited;

        /// <summary>Raised when the player confirms interaction with the current target.</summary>
        public event Action<IInteractable> OnInteract;

        /// <summary>The interactable currently in range, or null.</summary>
        public IInteractable Current => _current;

        private void Awake()
        {
            _hits = new Collider[Mathf.Max(1, _maxColliders)];
            CacheInputAction();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);

            if (_interactAction != null)
            {
                _interactAction.performed += OnInteractPerformed;
                _interactAction.Enable();
            }
            _scanRoutine = StartCoroutine(CoScan());
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);

            if (_interactAction != null)
            {
                _interactAction.performed -= OnInteractPerformed;
                _interactAction.Disable();
            }
            if (_scanRoutine != null)
            {
                StopCoroutine(_scanRoutine);
                _scanRoutine = null;
            }
            ClearCurrent();
        }

        private void CacheInputAction()
        {
            if (_inputActions == null) return;
            var map = _inputActions.FindActionMap(_actionMap, throwIfNotFound: false);
            _interactAction = map?.FindAction(INTERACT_ACTION, throwIfNotFound: false);
            if (_interactAction == null)
                Debug.LogError($"[InteractionSystem] '{INTERACT_ACTION}' action not found on map '{_actionMap}'.");
        }

        /// <summary>Periodic detection loop. Kept out of Update so non-movement logic isn't frame-bound.</summary>
        private IEnumerator CoScan()
        {
            var wait = _scanInterval > 0f ? new WaitForSeconds(_scanInterval) : null;
            while (true)
            {
                if (_enabled) Scan();
                if (wait != null) yield return wait;
                else yield return null;
            }
        }

        /// <summary>One detection pass via Physics.OverlapSphere; picks the nearest valid interactable.</summary>
        private void Scan()
        {
            Vector3 center = transform.TransformPoint(_detectionOffset);
            int count = Physics.OverlapSphereNonAlloc(
                center, _detectionRadius, _hits, _interactableMask, QueryTriggerInteraction.Collide);

            IInteractable nearest = null;
            float nearestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = _hits[i];
                if (col == null) continue;

                var interactable = col.GetComponentInParent<IInteractable>();
                if (interactable == null) continue;

                float sqr = (col.transform.position - center).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = interactable;
                }
            }

            if (!ReferenceEquals(nearest, _current))
                SetCurrent(nearest);
        }

        private void SetCurrent(IInteractable next)
        {
            if (_current != null)
                OnInteractableExited?.Invoke(_current);

            _current = next;

            if (_current != null)
                OnInteractableEntered?.Invoke(_current);
        }

        private void ClearCurrent()
        {
            if (_current == null) return;
            OnInteractableExited?.Invoke(_current);
            _current = null;
        }

        private void OnInteractPerformed(InputAction.CallbackContext _)
        {
            if (!_enabled || _current == null) return;

            _current.Interact(gameObject);
            OnInteract?.Invoke(_current);
        }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            _enabled = e.next == GameState.Exploring;
            if (!_enabled) ClearCurrent();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.TransformPoint(_detectionOffset), _detectionRadius);
        }
    }
}
