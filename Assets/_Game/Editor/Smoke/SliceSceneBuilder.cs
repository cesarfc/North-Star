#if UNITY_EDITOR
using NorthStar.Inventory;
using NorthStar.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Builds the Phase 4 vertical-slice scene (SCN_VerticalSlice): the player loop plus three
/// complete systems wired together — a running DayNightCycle, an item Pickup that feeds the
/// Inventory, and a HUD reading live PlayerStats/Inventory/DayNight state. Private serialized
/// fields are wired via SerializedObject. Invoke headless:
///   Unity -batchmode -quit -projectPath . -executeMethod SliceSceneBuilder.Build
/// </summary>
public static class SliceSceneBuilder
{
    private const string SceneDir = "Assets/_Game/Scenes";
    private const string ScenePath = SceneDir + "/SCN_VerticalSlice.unity";
    private const string SmokeScenePath = SceneDir + "/SCN_Smoke.unity";
    private const string InputAssetPath = "Assets/Settings/PlayerInputActions.inputactions";
    private const string HealthPotionPath = "Assets/_Game/ScriptableObjects/Items/SO_Item_HealthPotion.asset";

    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Sun (driven by DayNightCycle)
        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sunGo.transform.rotation = Quaternion.Euler(50f, 170f, 0f);

        // Ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(5f, 1f, 5f);

        // GameManager + enter-Exploring bootstrap
        var gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        gm.AddComponent<SmokeBootstrap>();

        // Day/Night cycle (fast time scale so the HUD clock visibly ticks in the demo)
        var dnGo = new GameObject("DayNightCycle");
        var dayNight = dnGo.AddComponent<DayNightCycle>();
        var dnSo = new SerializedObject(dayNight);
        dnSo.FindProperty("_sunLight").objectReferenceValue = sun;
        dnSo.FindProperty("_startHour").floatValue = 8f;
        dnSo.FindProperty("_timeScale").floatValue = 720f; // 1 game hour ≈ 5 real seconds
        dnSo.FindProperty("_autoAdvance").boolValue = true;
        dnSo.ApplyModifiedPropertiesWithoutUndo();

        // Player: controller + interaction + stats + inventory
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
        var stats = player.AddComponent<PlayerStats>();
        var inventory = player.AddComponent<Inventory>();

        // Third-person camera (child of player)
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.AddComponent<Camera>();
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 4f, -6f);
        camGo.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);

        var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputAssetPath);
        WireRef(pc, "_inputActions", inputAsset);
        WireRef(pc, "_cameraTransform", camGo.transform);
        WireRef(interaction, "_inputActions", inputAsset);

        // NPC (talk → save), reused from the smoke scene
        var npc = GameObject.CreatePrimitive(PrimitiveType.Cube);
        npc.name = "NPC";
        npc.transform.position = new Vector3(-3f, 0.5f, 5f);
        npc.AddComponent<SmokeNPC>();

        // Item pickup → Inventory
        var potion = AssetDatabase.LoadAssetAtPath<ItemData>(HealthPotionPath);
        var pickup = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        pickup.name = "Pickup_HealthPotion";
        pickup.transform.position = new Vector3(3f, 0.6f, 5f);
        pickup.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        var pickupComp = pickup.AddComponent<PickupItem>();
        WireRef(pickupComp, "_item", potion);
        SetInt(pickupComp, "_quantity", 1);
        WireRef(pickupComp, "_inventory", inventory);

        // Battle trigger — interacting runs a demo battle through the real Battle module
        var battle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        battle.name = "BattleTrigger";
        battle.transform.position = new Vector3(0f, 0.5f, 8f);
        battle.transform.localScale = new Vector3(1f, 1f, 1f);
        battle.AddComponent<BattleEncounter>();

        // HUD (reads the three systems)
        var hudGo = new GameObject("SliceHud");
        var hud = hudGo.AddComponent<SliceHud>();
        WireRef(hud, "_stats", stats);
        WireRef(hud, "_inventory", inventory);
        WireRef(hud, "_dayNight", dayNight);

        if (!AssetDatabase.IsValidFolder(SceneDir))
            AssetDatabase.CreateFolder("Assets/_Game", "Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SmokeScenePath, true),
            new EditorBuildSettingsScene(ScenePath, true),
        };

        Debug.Log("[NSSetup] SLICE_SCENE_OK -> " + ScenePath);
        EditorApplication.Exit(0);
    }

    private static void WireRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[NSSetup] {target.GetType().Name}.{field} not found"); return; }
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetInt(Object target, string field, int value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[NSSetup] {target.GetType().Name}.{field} not found"); return; }
        prop.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
