using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Drives scripted cutscenes: it raises the game into <see cref="GameState.Cutscene"/>
/// (which the Player module listens for to disable input — there is no direct reference
/// across modules) and sequences a list of <see cref="CutsceneShot"/>s by toggling
/// Cinemachine 3.x camera priorities so the brain blends between them.
///
/// Cross-module coupling is EventBus-only: this director publishes/consumes state via the
/// bus and never touches another module's MonoBehaviour. Cinemachine is a package, not a
/// module, so it is referenced directly per the contract.
/// </summary>
public class CutsceneDirector : MonoBehaviour
{
    [Header("Shots")]
    [SerializeField] private CutsceneShot[] _shots;

    [Header("Priorities")]
    [Tooltip("Priority assigned to the active cutscene camera so it wins the blend.")]
    [SerializeField] private int _activePriority = 100;

    [Tooltip("Priority restored to a cutscene camera once its beat is over.")]
    [SerializeField] private int _inactivePriority = 0;

    private Coroutine _playRoutine;
    private GameState _stateBeforeCutscene = GameState.Exploring;

    /// <summary>True while a cutscene sequence is playing.</summary>
    public bool IsPlaying { get; private set; }

    /// <summary>
    /// Begin the authored shot sequence. Switches to the Cutscene state (player input is
    /// disabled by the Player module reacting to that state) and blends through each shot.
    /// No-op if a cutscene is already playing or there are no shots.
    /// </summary>
    public void PlayCutscene()
    {
        if (IsPlaying)
        {
            Debug.LogWarning("[CutsceneDirector] PlayCutscene ignored — a cutscene is already playing.");
            return;
        }
        if (_shots == null || _shots.Length == 0)
        {
            Debug.LogWarning("[CutsceneDirector] PlayCutscene called with no shots configured.");
            return;
        }

        IsPlaying = true;
        EnterCutsceneState();
        _playRoutine = StartCoroutine(CoPlay());
    }

    /// <summary>
    /// Stop the cutscene immediately, lower all cutscene cameras, and restore the prior
    /// game state (re-enabling player input). Safe to call when nothing is playing.
    /// </summary>
    public void StopCutscene()
    {
        if (!IsPlaying) return;

        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        LowerAllCameras();
        IsPlaying = false;
        ExitCutsceneState();
    }

    private IEnumerator CoPlay()
    {
        foreach (var shot in _shots)
        {
            ActivateShot(shot);

            if (shot.holdSeconds > 0f)
                yield return new WaitForSeconds(shot.holdSeconds);
            else
                yield return null; // 0 = single-frame; manual advance handled by caller via Stop/replay

            if (shot.camera != null)
                shot.camera.Priority = _inactivePriority;
        }

        _playRoutine = null;
        LowerAllCameras();
        IsPlaying = false;
        ExitCutsceneState();
    }

    private void ActivateShot(CutsceneShot shot)
    {
        if (shot.camera == null)
        {
            Debug.LogWarning("[CutsceneDirector] Encountered a shot with no camera assigned; skipping.");
            return;
        }
        shot.camera.Priority = _activePriority;
    }

    private void LowerAllCameras()
    {
        if (_shots == null) return;
        foreach (var shot in _shots)
            if (shot.camera != null)
                shot.camera.Priority = _inactivePriority;
    }

    private void EnterCutsceneState()
    {
        if (GameManager.Instance == null) return;
        _stateBeforeCutscene = GameManager.Instance.CurrentState;
        GameManager.Instance.ChangeState(GameState.Cutscene);
    }

    private void ExitCutsceneState()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState == GameState.Cutscene)
            GameManager.Instance.ChangeState(_stateBeforeCutscene);
    }
}
