using NorthStar.Audio;
using NorthStar.Player;
using UnityEngine;

/// <summary>
/// Distance-based footstep driver for the slice: while the player is moving, calls the Audio
/// module's <see cref="FootstepSystem.Step"/> once per stride length, which raycasts the
/// surface and plays the mapped footstep clipId through the AudioManager. Update() usage is
/// movement-cadence tracking, per the CONVENTIONS exception for movement/rendering.
/// Composition-root glue (NorthStar.Game).
/// </summary>
public class SliceFootsteps : MonoBehaviour
{
    [SerializeField] private FootstepSystem _footsteps;
    [SerializeField] private PlayerController _player;

    [Tooltip("Meters travelled between footstep sounds.")]
    [SerializeField] private float _strideMeters = 1.9f;

    private Vector3 _lastPosition;
    private float _travelled;

    private void OnEnable()
    {
        _lastPosition = transform.position;
    }

    private void Update()
    {
        if (_footsteps == null || _player == null) return;

        Vector3 position = transform.position;
        if (!_player.IsMoving)
        {
            _lastPosition = position;
            return;
        }

        Vector3 delta = position - _lastPosition;
        delta.y = 0f;
        _travelled += delta.magnitude;
        _lastPosition = position;

        if (_travelled < _strideMeters) return;
        _travelled = 0f;
        _footsteps.Step();
    }
}
