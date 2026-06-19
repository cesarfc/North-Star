#if UNITY_EDITOR
using NorthStar.Character;
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
    private const string Zone02ScenePath = SceneDir + "/SCN_Zone02.unity";
    private const string ZoneAssetPath = "Assets/_Game/ScriptableObjects/Zones/SO_Zone_Slice02.asset";

    public static void Build()
    {
        // Target zone for the gate: a WorldZoneData asset pointing at the second scene.
        var zoneAsset = AssetDatabase.LoadAssetAtPath<WorldZoneData>(ZoneAssetPath);
        if (zoneAsset == null)
        {
            zoneAsset = ScriptableObject.CreateInstance<WorldZoneData>();
            zoneAsset.zoneId = "zone-slice-02";
            zoneAsset.displayName = "Outpost";
            zoneAsset.sceneId = "SCN_Zone02";
            AssetDatabase.CreateAsset(zoneAsset, ZoneAssetPath);
            AssetDatabase.SaveAssets();
        }

        // Build the second zone first (NewScene below replaces it with the slice).
        BuildZone02();

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
        player.tag = "Player"; // so the ZoneTransition trigger detects the player

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

        // Zone gate — a pass-through trigger that additively loads SCN_Zone02 (World module)
        var gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gate.name = "ZoneGate";
        gate.transform.position = new Vector3(0f, 1f, 12f);
        gate.transform.localScale = new Vector3(3f, 2f, 0.3f);
        gate.GetComponent<Collider>().isTrigger = true; // pass through; OnTriggerEnter fires
        var zt = gate.AddComponent<ZoneTransition>();
        WireRef(zt, "_targetZone", zoneAsset);
        SetString(zt, "_spawnPointId", "spawn-outpost");
        SetString(zt, "_fromZoneId", "zone-slice-01");

        var bannerGo = new GameObject("ZoneBanner");
        bannerGo.AddComponent<ZoneBanner>();

        // Character customization station (drives CharacterCustomizer on the player)
        var customizer = player.AddComponent<CharacterCustomizer>();
        var armors = new[]
        {
            AssetDatabase.LoadAssetAtPath<ArmorData>("Assets/_Game/ScriptableObjects/Armor/SO_Armor_LightChest.asset"),
            AssetDatabase.LoadAssetAtPath<ArmorData>("Assets/_Game/ScriptableObjects/Armor/SO_Armor_MediumChest.asset"),
            AssetDatabase.LoadAssetAtPath<ArmorData>("Assets/_Game/ScriptableObjects/Armor/SO_Armor_HeavyChest.asset"),
        };
        var hairs = new[]
        {
            AssetDatabase.LoadAssetAtPath<HairStyleData>("Assets/_Game/ScriptableObjects/Hair/SO_Hair_ShortCrop.asset"),
            AssetDatabase.LoadAssetAtPath<HairStyleData>("Assets/_Game/ScriptableObjects/Hair/SO_Hair_LongBraid.asset"),
        };
        var charStation = GameObject.CreatePrimitive(PrimitiveType.Cube);
        charStation.name = "CharacterStation";
        charStation.transform.position = new Vector3(-6f, 0.5f, 2f);
        var cs = charStation.AddComponent<CharacterStation>();
        WireRef(cs, "_customizer", customizer);
        WireArray(cs, "_armors", armors);
        WireArray(cs, "_hairs", hairs);

        // Shop station (drives ShopUI → gold via EventBus + inventory)
        var shopGo = new GameObject("ShopUI");
        var shopUI = shopGo.AddComponent<ShopUI>();
        WireRef(shopUI, "_inventory", inventory);
        var shopItems = new[]
        {
            AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Game/ScriptableObjects/Items/SO_Item_HealthPotion.asset"),
            AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Game/ScriptableObjects/Items/SO_Item_ManaPotion.asset"),
            AssetDatabase.LoadAssetAtPath<ItemData>("Assets/_Game/ScriptableObjects/Items/SO_Item_IronSword.asset"),
        };
        var shopStationGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shopStationGo.name = "ShopStation";
        shopStationGo.transform.position = new Vector3(6f, 0.5f, 2f);
        var ss = shopStationGo.AddComponent<ShopStation>();
        WireRef(ss, "_shop", shopUI);
        WireRef(ss, "_stats", stats);
        WireArray(ss, "_forSale", shopItems);

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
            new EditorBuildSettingsScene(Zone02ScenePath, true),
        };

        Debug.Log("[NSSetup] SLICE_SCENE_OK -> " + ScenePath);
        EditorApplication.Exit(0);
    }

    /// <summary>Builds the additively-loaded second zone (SCN_Zone02), offset from the slice.</summary>
    private static void BuildZone02()
    {
        var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lightGo = new GameObject("Sun");
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, 200f, 0f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground (Outpost)";
        ground.transform.position = new Vector3(60f, 0f, 0f); // offset so it doesn't overlap the slice
        ground.transform.localScale = new Vector3(3f, 1f, 3f);

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "OutpostMarker";
        marker.transform.position = new Vector3(60f, 1f, 0f);

        var label = new GameObject("ZoneLabel");
        var zl = label.AddComponent<ZoneLabel>();
        SetString(zl, "_text", "ZONE 02 — Outpost (additively loaded)");

        if (!AssetDatabase.IsValidFolder(SceneDir))
            AssetDatabase.CreateFolder("Assets/_Game", "Scenes");
        EditorSceneManager.SaveScene(s, Zone02ScenePath);
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

    private static void SetString(Object target, string field, string value)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[NSSetup] {target.GetType().Name}.{field} not found"); return; }
        prop.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogError($"[NSSetup] {target.GetType().Name}.{field} not found"); return; }
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
