using UnityEngine;
using UnityEngine.EventSystems;

namespace NorthStar.Character
{
    /// <summary>
    /// A UI panel that lets the player rotate the character preview model by dragging
    /// on it. Attach to the preview <c>RectTransform</c> (typically over a RawImage fed by
    /// a render-texture camera, or an in-world model framed by a preview camera). Horizontal
    /// drag spins the <see cref="_previewTarget"/> around its up axis; optional vertical drag
    /// adds clamped pitch.
    ///
    /// Pointer drag is handled through the UI <see cref="EventSystems"/> callbacks (the standard,
    /// New-Input-System-compatible UI path) — it does not poll legacy <c>Input.GetAxis</c>.
    /// </summary>
    public class CharacterPreviewUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Target")]
        [Tooltip("The model transform that drag-rotation is applied to.")]
        [SerializeField] private Transform _previewTarget;

        [Header("Rotation")]
        [Tooltip("Degrees of yaw per pixel of horizontal drag.")]
        [SerializeField] private float _yawSpeed = 0.4f;
        [Tooltip("Degrees of pitch per pixel of vertical drag. Set 0 to lock pitch.")]
        [SerializeField] private float _pitchSpeed = 0f;
        [Tooltip("Pitch is clamped to +/- this many degrees from the start pitch.")]
        [SerializeField] private float _pitchClamp = 25f;

        [Tooltip("Optional auto-spin (deg/sec) applied while not dragging. 0 = off.")]
        [SerializeField] private float _idleSpinSpeed = 0f;

        private bool _isDragging;
        private float _yaw;
        private float _pitch;
        private float _basePitch;

        /// <summary>True while the user is actively dragging the model.</summary>
        public bool IsDragging => _isDragging;

        private void OnEnable()
        {
            if (_previewTarget != null)
            {
                Vector3 euler = _previewTarget.localEulerAngles;
                _yaw = euler.y;
                _pitch = NormalizeAngle(euler.x);
                _basePitch = _pitch;
            }
        }

        private void Update()
        {
            if (_isDragging || _idleSpinSpeed == 0f || _previewTarget == null) return;
            _yaw += _idleSpinSpeed * Time.deltaTime;
            ApplyRotation();
        }

        /// <summary>UI callback: begin a drag gesture.</summary>
        public void OnBeginDrag(PointerEventData eventData) => _isDragging = true;

        /// <summary>UI callback: end the current drag gesture.</summary>
        public void OnEndDrag(PointerEventData eventData) => _isDragging = false;

        /// <summary>UI callback: convert pointer delta into yaw/pitch on the preview model.</summary>
        public void OnDrag(PointerEventData eventData)
        {
            if (_previewTarget == null) return;

            // Drag right -> model turns to face its left, i.e. negative world yaw feels natural.
            _yaw -= eventData.delta.x * _yawSpeed;

            if (_pitchSpeed != 0f)
            {
                _pitch += eventData.delta.y * _pitchSpeed;
                _pitch = Mathf.Clamp(_pitch, _basePitch - _pitchClamp, _basePitch + _pitchClamp);
            }

            ApplyRotation();
        }

        private void ApplyRotation()
        {
            _previewTarget.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            return angle;
        }
    }
}
