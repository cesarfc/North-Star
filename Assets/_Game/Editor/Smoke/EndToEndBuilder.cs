#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace NorthStar.EditorTools
{
    /// <summary>
    /// One-shot end-to-end content build, chaining every builder in dependency order inside a
    /// single editor session: wardrobe ScriptableObjects from the character-pack manifest →
    /// SCN_Zone02 + SCN_VerticalSlice (terrain/props/grass from the land pack, dressed rig,
    /// dialogue/battle/audio wiring) → SCN_Boot (main menu, registered first in Build Settings).
    /// Headless: <c>-executeMethod NorthStar.EditorTools.EndToEndBuilder.BuildAll</c>
    /// (exits 0/1; greps: [AssetLib], [Env], [NSSetup], END_TO_END_BUILD).
    /// </summary>
    public static class EndToEndBuilder
    {
        /// <summary>Build wardrobe assets + all scenes, then exit with a pass/fail code.</summary>
        public static void BuildAll()
        {
            bool assetsOk = CharacterAssetLibraryBuilder.Build();
            SliceSceneBuilder.BuildScene();
            BootSceneBuilder.BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log($"[NSSetup] END_TO_END_BUILD_{(assetsOk ? "OK" : "FAIL")}");
            EditorApplication.Exit(assetsOk ? 0 : 1);
        }
    }
}
#endif
