using System;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// A single framed beat in a cutscene: which Cinemachine camera to raise to the top of
/// the blend stack and how long to hold it before advancing. Authored in the Inspector
/// on <see cref="CutsceneDirector"/>.
/// </summary>
[Serializable]
public struct CutsceneShot
{
    [Tooltip("Cinemachine 3.x camera to activate for this beat (raised via priority).")]
    public CinemachineCamera camera;

    [Tooltip("Seconds to hold this shot before moving to the next one. 0 = hold until manually advanced.")]
    public float holdSeconds;
}
