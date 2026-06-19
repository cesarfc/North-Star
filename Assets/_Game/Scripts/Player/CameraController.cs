using Unity.Cinemachine;
using UnityEngine;

namespace NorthStar.Player
{
    /// <summary>
    /// Third-person camera coordinator built on Cinemachine 3.x. It does NOT
    /// implement orbit/collision math itself — that is delegated to Cinemachine
    /// components on the assigned <see cref="CinemachineCamera"/> rigs (an
    /// <c>CinemachineOrbitalFollow</c> for orbit and a <c>CinemachineDeoccluder</c>
    /// for collision). This class only wires the follow/look targets and switches
    /// between the free-look rig and the lock-on rig by adjusting camera priority.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Cinemachine Rigs (3.x)")]
        [Tooltip("Default third-person free-look camera. Should carry CinemachineOrbitalFollow + CinemachineDeoccluder.")]
        [SerializeField] private CinemachineCamera _freeLookCamera;
        [Tooltip("Optional dedicated lock-on camera. Falls back to retargeting the free-look cam if unset.")]
        [SerializeField] private CinemachineCamera _lockOnCamera;

        [Header("Targets")]
        [Tooltip("Transform the camera follows (usually the player root or a camera pivot).")]
        [SerializeField] private Transform _followTarget;
        [Tooltip("Transform the camera looks at while exploring (usually the player head/torso).")]
        [SerializeField] private Transform _lookTarget;

        [Header("Priorities")]
        [SerializeField] private int _activePriority = 20;
        [SerializeField] private int _inactivePriority = 0;

        private bool _isLockedOn;

        /// <summary>True while the lock-on rig is the live camera.</summary>
        public bool IsLockedOn => _isLockedOn;

        private void Awake()
        {
            ApplyTargets();
            ActivateFreeLook();
        }

        /// <summary>Push follow/look targets onto whichever rigs are assigned.</summary>
        private void ApplyTargets()
        {
            if (_freeLookCamera != null)
            {
                _freeLookCamera.Follow = _followTarget;
                _freeLookCamera.LookAt = _lookTarget != null ? _lookTarget : _followTarget;
            }
            if (_lockOnCamera != null)
                _lockOnCamera.Follow = _followTarget;
        }

        /// <summary>Make the standard free-look rig the live camera.</summary>
        public void ActivateFreeLook()
        {
            _isLockedOn = false;
            SetPriority(_freeLookCamera, _activePriority);
            SetPriority(_lockOnCamera, _inactivePriority);
        }

        /// <summary>
        /// Frame a lock-on target. Uses the dedicated lock-on rig if present,
        /// otherwise points the free-look rig's LookAt at the target.
        /// </summary>
        /// <param name="target">The enemy/anchor to frame. Null clears lock-on.</param>
        public void SetLockOnTarget(Transform target)
        {
            if (target == null)
            {
                ClearLockOn();
                return;
            }

            _isLockedOn = true;

            if (_lockOnCamera != null)
            {
                _lockOnCamera.LookAt = target;
                SetPriority(_lockOnCamera, _activePriority);
                SetPriority(_freeLookCamera, _inactivePriority);
            }
            else if (_freeLookCamera != null)
            {
                _freeLookCamera.LookAt = target;
            }
        }

        /// <summary>Drop any active lock-on and return to free-look framing.</summary>
        public void ClearLockOn()
        {
            ActivateFreeLook();
            if (_freeLookCamera != null)
                _freeLookCamera.LookAt = _lookTarget != null ? _lookTarget : _followTarget;
        }

        /// <summary>Re-point the camera at a new follow target (e.g. after a respawn/teleport).</summary>
        public void SetFollowTarget(Transform follow, Transform look = null)
        {
            _followTarget = follow;
            if (look != null) _lookTarget = look;
            ApplyTargets();
        }

        private void SetPriority(CinemachineCamera cam, int priority)
        {
            if (cam != null) cam.Priority = priority;
        }
    }
}
