#if UNITY_EDITOR
using NorthStar.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Builds the Phase 1 smoke-test scene (SCN_Smoke) from scratch and wires the Player module's
/// private serialized fields via SerializedObject. Invoke headless:
///   Unity -batchmode -quit -projectPath . -executeMethod SmokeSceneBuilder.Build
/// </summary>
public static class SmokeSceneBuilder
{
    private const string SceneDir = "Assets/_Game/Scenes";
    private const string ScenePath = SceneDir + "/SCN_Smoke.unity";
    private const string InputAssetPath = "Assets/Settings/PlayerInputActions.inputactions";

    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Light
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(5f, 1f, 5f);

        // GameManager (+ smoke bootstrap that enters Exploring on play)
        var gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        gm.AddComponent<SmokeBootstrap>();

        // Player: CharacterController + PlayerController + InteractionSystem, with a visual capsule
        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, 1f, 0f);
        var cc = player.AddComponent<CharacterController>();
        cc.center = new Vector3(0f, 1f, 0f);
        cc.height = 2f;
        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        var pc = player.AddComponent<PlayerController>();
        var interaction = player.AddComponent<InteractionSystem>();

        // Third-person camera as a child of the player (no Cinemachine needed for the smoke test)
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 4f, -6f);
        camGo.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);

        // NPC: a cube (BoxCollider for OverlapSphere detection) carrying SmokeNPC
        var npc = GameObject.CreatePrimitive(PrimitiveType.Cube);
        npc.name = "NPC";
        npc.transform.position = new Vector3(0f, 0.5f, 5f);
        npc.AddComponent<SmokeNPC>();

        // Wire the Player module's private [SerializeField] references.
        var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
        if (inputAsset == null)
            Debug.LogWarning("[NSSetup] Input actions asset not found at " + InputAssetPath);

        WireRef(pc, "_inputActions", inputAsset);
        WireRef(pc, "_cameraTransform", camGo.transform);
        WireRef(interaction, "_inputActions", inputAsset);

        // Save + register in build settings as scene 0
        if (!AssetDatabase.IsValidFolder(SceneDir))
            AssetDatabase.CreateFolder("Assets/_Game", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        Debug.Log("[NSSetup] SMOKE_SCENE_OK -> " + ScenePath);
        EditorApplication.Exit(0);
    }

    private static void WireRef(Object target, string fieldName, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[NSSetup] Field '{fieldName}' not found on {target.GetType().Name} — wiring skipped.");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
