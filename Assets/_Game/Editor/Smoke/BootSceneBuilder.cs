#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NorthStar.EditorTools
{
    /// <summary>
    /// Builds SCN_Boot — the entry scene: a persistent <see cref="GameManager"/> (starting in
    /// MainMenu state), the <see cref="MainMenuUI"/> (New Game / Continue / Quit), and the
    /// dressed rig from the character pack posed in front of the camera as menu dressing.
    /// Registers the scene at index 0 of Build Settings so the player build boots into it.
    /// Headless: <c>-executeMethod NorthStar.EditorTools.BootSceneBuilder.Build</c>.
    /// </summary>
    public static class BootSceneBuilder
    {
        private const string ScenePath = "Assets/_Game/Scenes/SCN_Boot.unity";

        /// <summary>Headless entry point: build the boot scene, then quit the editor.</summary>
        public static void Build()
        {
            BuildScene();
            EditorApplication.Exit(0);
        }

        /// <summary>Build SCN_Boot and put it first in Build Settings (non-exiting).</summary>
        [MenuItem("Tools/North-Star/Build Boot Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.14f); // night sky behind the title
            camGo.transform.position = new Vector3(0.6f, 1.3f, 2.6f);
            camGo.transform.LookAt(new Vector3(0f, 0.95f, 0f));

            var lightGo = new GameObject("Key Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.85f);
            lightGo.transform.rotation = Quaternion.Euler(35f, 205f, 0f);

            var gm = new GameObject("GameManager");
            gm.AddComponent<GameManager>();

            var menuGo = new GameObject("MainMenuUI");
            menuGo.AddComponent<MainMenuUI>();

            // Menu dressing: the dressed adventurer looking at the camera. Reuses the slice rig
            // assembler; the CharacterCustomizer it wires is idle here (no stations in this scene).
            NorthStarManifest characters = NorthStarManifest.Load();
            var anchor = new GameObject("MenuCharacter");
            anchor.transform.position = new Vector3(0f, 1f, 0f); // rig assembler drops feet to Y=0
            anchor.transform.rotation = Quaternion.Euler(0f, 15f, 0f);
            if (SliceCharacterRig.Attach(anchor, characters) == null)
                Object.DestroyImmediate(anchor);

            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterFirstInBuildSettings();
            Debug.Log("[NSSetup] BOOT_SCENE_OK -> " + ScenePath);
        }

        /// <summary>Put SCN_Boot at build index 0, preserving the other registered scenes.</summary>
        private static void RegisterFirstInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
                .Where(s => s.path != ScenePath)
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
