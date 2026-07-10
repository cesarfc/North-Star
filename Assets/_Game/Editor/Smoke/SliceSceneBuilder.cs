#if UNITY_EDITOR
using NorthStar.Audio;
using NorthStar.Character;
using NorthStar.EditorTools;
using NorthStar.Inventory;
using NorthStar.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Builds the vertical-slice scene (SCN_VerticalSlice): the full player loop on real assets —
/// factory-generated terrain/props/grass (land pack), the rigged, dressed chibi character with
/// the complete wardrobe (character pack), plus the running DayNightCycle, pickups feeding the
/// Inventory, a battle encounter, a zone gate into SCN_Zone02, shop + customization stations and
/// the HUD. Private serialized fields are wired via SerializedObject. Invoke headless:
///   Unity -batchmode -projectPath . -executeMethod SliceSceneBuilder.Build
/// (or chain without exiting via <see cref="BuildScene"/> from another builder).
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
    private const string ArmorDir = "Assets/_Game/ScriptableObjects/Armor";
    private const string HairDir = "Assets/_Game/ScriptableObjects/Hair";

    /// <summary>Where the slice zone's flat gameplay plateau sits (surface at world Y = 0).</summary>
    private static readonly SliceEnvironmentBuilder.TerrainSpec SliceTerrain = new SliceEnvironmentBuilder.TerrainSpec
    {
        name = "Terrain_Slice",
        worldCenter = Vector2.zero,
        size = 120f,
        plateauRadius = 26f,
        seed = 42,
    };

    /// <summary>Zone02 ("Outpost") terrain, east of the slice bowl — reached via teleporting gate.</summary>
    private static readonly SliceEnvironmentBuilder.TerrainSpec OutpostTerrain = new SliceEnvironmentBuilder.TerrainSpec
    {
        name = "Terrain_Outpost",
        worldCenter = new Vector2(100f, 0f),
        size = 80f,
        plateauRadius = 20f,
        seed = 77,
    };

    /// <summary>Center of Zone02's gameplay plateau (marker, label, spawn point live here).</summary>
    private static readonly Vector3 OutpostCenter = new Vector3(100f, 0f, 0f);

    /// <summary>Headless entry point: build both scenes, then quit the editor.</summary>
    public static void Build()
    {
        BuildScene();
        EditorApplication.Exit(0);
    }

    /// <summary>Build SCN_Zone02 + SCN_VerticalSlice and register them in Build Settings
    /// (non-exiting, so a master builder can chain more steps in the same session).</summary>
    public static void BuildScene()
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

        NorthStarLandManifest land = NorthStarLandManifest.Load();
        NorthStarManifest characters = NorthStarManifest.Load();

        // Build the second zone first (NewScene below replaces it with the slice).
        BuildZone02(land);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Sun (driven by DayNightCycle)
        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.1f;
        sunGo.transform.rotation = Quaternion.Euler(50f, 170f, 0f);

        // Environment: factory land pack → terrain + prop scatter + GPU-instanced grass.
        if (land != null)
        {
            Terrain terrain = SliceEnvironmentBuilder.BuildTerrain(SliceTerrain, land);
            SliceEnvironmentBuilder.ScatterProps(terrain, SliceTerrain, land, seed: 4207);
            SliceEnvironmentBuilder.AddGrass(terrain, SliceTerrain, land, instanceCount: 6000);
        }
        else
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground (fallback — land pack missing)";
            ground.transform.localScale = new Vector3(5f, 1f, 5f);
        }

        // GameManager + enter-Exploring bootstrap
        var gm = new GameObject("GameManager");
        gm.AddComponent<GameManager>();
        gm.AddComponent<SmokeBootstrap>();

        // Audio service (Audio module): pooled SFX + zone-music auto-crossfade. It self-subscribes
        // to ZoneEnteredEvent, so simply existing in the scene makes the ZoneGate trigger music.
        var audioGo = new GameObject("AudioManager");
        var audioManager = audioGo.AddComponent<AudioManager>();

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
        // Visual: the dressed rigged character from the art pack (LFS), else a capsule.
        CharacterCustomizer customizer = SliceCharacterRig.Attach(player, characters);
        if (customizer == null)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            customizer = player.AddComponent<CharacterCustomizer>();
        }
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

        // NPC — Elder Vane, the quest giver. Runs the real Dialogue module (Yarn Spinner)
        // when the package is installed; falls back to the hard-coded SmokeNPC otherwise.
        var npc = GameObject.CreatePrimitive(PrimitiveType.Cube);
        npc.name = "NPC_ElderVane";
        npc.transform.position = new Vector3(-3f, 0.5f, 5f);
        QuestManager quests = WireDialogue(npc);

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

        // Character customization station: the full factory wardrobe (worn + socket armor + hair).
        var armors = LoadAll<ArmorData>(ArmorDir,
            "SO_Armor_IronChestplate", "SO_Armor_ClothTunic", "SO_Armor_LeatherPants",
            "SO_Armor_LeatherBoots", "SO_Armor_IronHelmet",
            "SO_Armor_LightChest", "SO_Armor_MediumChest", "SO_Armor_HeavyChest");
        var hairs = LoadAll<HairStyleData>(HairDir, "SO_Hair_ShortCrop", "SO_Hair_LongBraid", "SO_Hair_Spiky");
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

        // HUD (reads the live systems)
        var hudGo = new GameObject("SliceHud");
        var hud = hudGo.AddComponent<SliceHud>();
        WireRef(hud, "_stats", stats);
        WireRef(hud, "_inventory", inventory);
        WireRef(hud, "_dayNight", dayNight);
        if (quests != null) WireRef(hud, "_quests", quests);

        // SFX glue (NorthStar.Game): plays pickup/battle SFX off ItemAddedEvent/BattleStartedEvent
        // through the AudioManager above. Silent until matching clipIds are registered.
        var sfxGo = new GameObject("SliceSfx");
        var sliceSfx = sfxGo.AddComponent<SliceSfx>();
        WireRef(sliceSfx, "_audioManager", audioManager);

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
    }

    /// <summary>Builds the additively-loaded second zone (SCN_Zone02) on its own terrain bowl,
    /// east of the slice, with the spawn point the zone gate teleports the player to.</summary>
    private static void BuildZone02(NorthStarLandManifest land)
    {
        var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var lightGo = new GameObject("Sun (Outpost)");
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(50f, 200f, 0f);

        if (land != null)
        {
            Terrain terrain = SliceEnvironmentBuilder.BuildTerrain(OutpostTerrain, land);
            SliceEnvironmentBuilder.ScatterProps(terrain, OutpostTerrain, land, seed: 7702);
            SliceEnvironmentBuilder.AddGrass(terrain, OutpostTerrain, land, instanceCount: 3000);
        }
        else
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground (Outpost fallback)";
            ground.transform.position = OutpostCenter;
            ground.transform.localScale = new Vector3(3f, 1f, 3f);
        }

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "OutpostMarker";
        marker.transform.position = OutpostCenter + new Vector3(0f, 1f, 0f);

        // Where the zone gate places the player (ZoneTransition convention: SpawnPoint_[id]).
        var spawn = new GameObject("SpawnPoint_spawn-outpost");
        spawn.transform.position = OutpostCenter + new Vector3(-12f, 1f, 0f);
        spawn.transform.rotation = Quaternion.LookRotation(Vector3.right); // face the marker

        var label = new GameObject("ZoneLabel");
        var zl = label.AddComponent<ZoneLabel>();
        SetString(zl, "_text", "ZONE 02 — Outpost (additively loaded)");

        if (!AssetDatabase.IsValidFolder(SceneDir))
            AssetDatabase.CreateFolder("Assets/_Game", "Scenes");
        EditorSceneManager.SaveScene(s, Zone02ScenePath);
    }

    /// <summary>
    /// Wire the real dialogue stack onto the NPC: Yarn DialogueRunner + the module's
    /// YarnDialogueRunner presenter → DialogueSystem → DialogueNPC/SliceDialogueUI, plus the
    /// QuestManager and the dialogue→quest bridge. Returns the QuestManager (for the HUD), or
    /// falls back to SmokeNPC (returning null) when Yarn Spinner isn't installed/imported.
    /// </summary>
    private static QuestManager WireDialogue(GameObject npc)
    {
#if YARN_SPINNER
        var project = AssetDatabase.LoadAssetAtPath<Yarn.Unity.YarnProject>("Assets/_Game/Dialogue/NorthStar.yarnproject");
        if (project == null)
        {
            Debug.LogWarning("[NSSetup] Yarn project not imported — NPC falls back to SmokeNPC.");
            npc.AddComponent<SmokeNPC>();
            return null;
        }

        var rig = new GameObject("DialogueRig");
        var yarnRunner = rig.AddComponent<Yarn.Unity.DialogueRunner>();
        var adapter = rig.AddComponent<YarnDialogueRunner>();
        var runnerSo = new SerializedObject(yarnRunner);
        runnerSo.FindProperty("yarnProject").objectReferenceValue = project;
        runnerSo.FindProperty("autoStart").boolValue = false;
        SerializedProperty presenters = runnerSo.FindProperty("dialoguePresenters");
        presenters.arraySize = 1;
        presenters.GetArrayElementAtIndex(0).objectReferenceValue = adapter;
        runnerSo.ApplyModifiedPropertiesWithoutUndo();
        WireRef(adapter, "_runner", yarnRunner);

        var dialogueGo = new GameObject("DialogueSystem");
        var dialogue = dialogueGo.AddComponent<DialogueSystem>();
        WireRef(dialogue, "_runnerBehaviour", adapter);

        var dialogueNpc = npc.AddComponent<DialogueNPC>();
        WireRef(dialogueNpc, "_dialogue", dialogue);

        var uiGo = new GameObject("SliceDialogueUI");
        var dialogueUi = uiGo.AddComponent<SliceDialogueUI>();
        WireRef(dialogueUi, "_dialogue", dialogue);

        var questsGo = new GameObject("QuestManager");
        var quests = questsGo.AddComponent<QuestManager>();
        WireArray(quests, "_questDatabase", new Object[]
        {
            AssetDatabase.LoadAssetAtPath<QuestData>("Assets/_Game/ScriptableObjects/Quests/SO_Quest_FindTheSpark.asset"),
            AssetDatabase.LoadAssetAtPath<QuestData>("Assets/_Game/ScriptableObjects/Quests/SO_Quest_MendTheBeacon.asset"),
        });
        var bridge = questsGo.AddComponent<DialogueQuestBridge>();
        WireRef(bridge, "_quests", quests);
        return quests;
#else
        npc.AddComponent<SmokeNPC>();
        return null;
#endif
    }

    private static T[] LoadAll<T>(string dir, params string[] names) where T : Object
    {
        var list = new System.Collections.Generic.List<T>(names.Length);
        foreach (string n in names)
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{dir}/{n}.asset");
            if (asset != null) list.Add(asset);
            else Debug.LogWarning($"[NSSetup] wardrobe asset missing: {dir}/{n}.asset");
        }
        return list.ToArray();
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
