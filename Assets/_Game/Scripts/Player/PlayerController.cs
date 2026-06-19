using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NorthStar.Player
{
    /// <summary>
    /// Third-person character movement: WASD/left-stick locomotion, sprint, jump
    /// and roll, driven by the New Input System. Reads the active camera to make
    /// movement camera-relative. Subscribes to <see cref="GameStateChangedEvent"/>
    /// and freezes input while the game is in Battle or Cutscene state.
    /// Movement is the one place an Update loop is allowed (CLAUDE.md).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("The shared .inputactions asset. The 'Exploration' map drives movement.")]
        [SerializeField] private InputActionAsset _inputActions;

        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 4f;
        [SerializeField] private float _sprintSpeed = 7f;
        [SerializeField] private float _rotationSmoothTime = 0.1f;
        [SerializeField] private float _acceleration = 20f;

        [Header("Jump & Gravity")]
        [SerializeField] private float _jumpHeight = 1.4f;
        [SerializeField] private float _gravity = -20f;
        [Tooltip("Downward force kept on the controller while grounded to stay snapped to the floor.")]
        [SerializeField] private float _groundedStick = -2f;

        [Header("Roll")]
        [SerializeField] private float _rollSpeed = 9f;
        [SerializeField] private float _rollDuration = 0.5f;
        [SerializeField] private float _rollCooldown = 0.6f;

        [Header("References")]
        [Tooltip("Camera transform used to make movement camera-relative. Falls back to Camera.main.")]
        [SerializeField] private Transform _cameraTransform;

        private const string EXPLORATION_MAP = "Exploration";

        private CharacterController _controller;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _jumpAction;
        private InputAction _rollAction;

        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _rotationVelocity;
        private bool _movementEnabled = true;
        private bool _inputHooked;
        private bool _wasGrounded;
        private bool _isRolling;
        private float _lastRollTime = -999f;
        private Coroutine _rollRoutine;

        /// <summary>True when the player has meaningful horizontal movement this frame.</summary>
        public bool IsMoving { get; private set; }

        /// <summary>Raised the frame the player touches the ground after being airborne.</summary>
        public event Action OnLanded;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            CacheInputActions();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EnableExplorationInput();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            DisableExplorationInput();
        }

        private void Update()
        {
            if (!_movementEnabled)
            {
                // Still settle vertical velocity so we don't accumulate gravity while frozen.
                _horizontalVelocity = Vector3.zero;
                IsMoving = false;
                return;
            }

            ApplyGravity();
            HandleMovement();
            DetectLanding();
        }

        // ── Input wiring ──────────────────────────────────────────────────

        private void CacheInputActions()
        {
            if (_inputActions == null) return;

            var map = _inputActions.FindActionMap(EXPLORATION_MAP, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogError($"[PlayerController] Input map '{EXPLORATION_MAP}' not found on the assigned asset.");
                return;
            }

            _moveAction = map.FindAction("Move", throwIfNotFound: false);
            _sprintAction = map.FindAction("Sprint", throwIfNotFound: false);
            _jumpAction = map.FindAction("Jump", throwIfNotFound: false);
            _rollAction = map.FindAction("Roll", throwIfNotFound: false);
        }

        private void EnableExplorationInput()
        {
            if (_inputHooked) return;
            _inputHooked = true;

            if (_jumpAction != null) _jumpAction.performed += OnJumpPerformed;
            if (_rollAction != null) _rollAction.performed += OnRollPerformed;

            _moveAction?.Enable();
            _sprintAction?.Enable();
            _jumpAction?.Enable();
            _rollAction?.Enable();
        }

        private void DisableExplorationInput()
        {
            if (!_inputHooked) return;
            _inputHooked = false;

            if (_jumpAction != null) _jumpAction.performed -= OnJumpPerformed;
            if (_rollAction != null) _rollAction.performed -= OnRollPerformed;

            _moveAction?.Disable();
            _sprintAction?.Disable();
            _jumpAction?.Disable();
            _rollAction?.Disable();
        }

        // ── Movement ──────────────────────────────────────────────────────

        private void HandleMovement()
        {
            if (_isRolling) return;

            Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector3 desired = CameraRelative(input);

            bool sprinting = _sprintAction != null && _sprintAction.IsPressed();
            float targetSpeed = (sprinting ? _sprintSpeed : _walkSpeed) * Mathf.Clamp01(input.magnitude);
            Vector3 targetVelocity = desired * targetSpeed;

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetVelocity, _acceleration * Time.deltaTime);

            if (desired.sqrMagnitude > 0.0001f)
                RotateTowards(desired);

            IsMoving = _horizontalVelocity.sqrMagnitude > 0.01f;

            Vector3 motion = _horizontalVelocity + (Vector3.up * _verticalVelocity);
            _controller.Move(motion * Time.deltaTime);
        }

        /// <summary>Convert a 2D input vector into a world-space direction relative to the camera's yaw.</summary>
        private Vector3 CameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

            Transform cam = ResolveCamera();
            Vector3 forward = cam != null ? cam.forward : Vector3.forward;
            Vector3 right = cam != null ? cam.right : Vector3.right;

            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            return (forward * input.y + right * input.x).normalized;
        }

        private void RotateTowards(Vector3 direction)
        {
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float yaw = Mathf.SmoothDampAngle(
                transform.eulerAngles.y, targetYaw, ref _rotationVelocity, _rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private Transform ResolveCamera()
        {
            if (_cameraTransform != null) return _cameraTransform;
            if (Camera.main != null) _cameraTransform = Camera.main.transform;
            return _cameraTransform;
        }

        // ── Gravity, jump, landing ────────────────────────────────────────

        private void ApplyGravity()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = _groundedStick;
            else
                _verticalVelocity += _gravity * Time.deltaTime;
        }

        private void OnJumpPerformed(InputAction.CallbackContext _)
        {
            if (!_movementEnabled || _isRolling) return;
            if (!_controller.isGrounded) return;

            _verticalVelocity = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
        }

        private void DetectLanding()
        {
            bool grounded = _controller.isGrounded;
            if (grounded && !_wasGrounded)
                OnLanded?.Invoke();
            _wasGrounded = grounded;
        }

        // ── Roll ──────────────────────────────────────────────────────────

        private void OnRollPerformed(InputAction.CallbackContext _)
        {
            if (!_movementEnabled || _isRolling) return;
            if (Time.time - _lastRollTime < _rollCooldown) return;
            if (!_controller.isGrounded) return;

            _rollRoutine = StartCoroutine(CoRoll());
        }

        private IEnumerator CoRoll()
        {
            _isRolling = true;
            _lastRollTime = Time.time;

            // Roll toward current facing if no input, otherwise toward input.
            Vector2 input = _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector3 dir = input.sqrMagnitude > 0.0001f ? CameraRelative(input) : transform.forward;
            if (dir.sqrMagnitude > 0.0001f) RotateTowards(dir);

            float elapsed = 0f;
            while (elapsed < _rollDuration)
            {
                _verticalVelocity += _gravity * Time.deltaTime;
                Vector3 motion = (dir * _rollSpeed) + (Vector3.up * _verticalVelocity);
                _controller.Move(motion * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _horizontalVelocity = Vector3.zero;
            _isRolling = false;
        }

        // ── Game-state gating ─────────────────────────────────────────────

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            bool allow = e.next == GameState.Exploring;
            SetMovementEnabled(allow);
        }

        // ── Public API (INTERFACE.md) ─────────────────────────────────────

        /// <summary>Enable or disable player-driven movement and locomotion input.</summary>
        public void SetMovementEnabled(bool enabled)
        {
            _movementEnabled = enabled;

            if (!enabled)
            {
                _horizontalVelocity = Vector3.zero;
                IsMoving = false;
                if (_rollRoutine != null)
                {
                    StopCoroutine(_rollRoutine);
                    _rollRoutine = null;
                    _isRolling = false;
                }
                DisableExplorationInput();
            }
            else
            {
                EnableExplorationInput();
            }
        }

        /// <summary>Move the player to a world position, preserving rotation. Safe with CharacterController.</summary>
        public void SetPosition(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            ResetMotion();
        }

        /// <summary>Move and reorient the player atomically (used by zone spawns / cutscenes).</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = true;
            ResetMotion();
        }

        private void ResetMotion()
        {
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
            IsMoving = false;
        }
    }
}
