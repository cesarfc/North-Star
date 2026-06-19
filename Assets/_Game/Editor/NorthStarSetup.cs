#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// One-shot project bootstrap helpers, invoked from the command line via
/// <c>-executeMethod</c>. Adds the project's required packages (letting the editor
/// resolve versions compatible with this Unity), so we never hand-pin versions.
/// Editor-only; safe to keep in the repo for re-setup.
/// </summary>
public static class NorthStarSetup
{
    private static AddAndRemoveRequest _addReq;

    /// <summary>Adds URP, Cinemachine, Input System and Test Framework, then exits the editor.</summary>
    public static void AddCorePackages()
    {
        string[] toAdd =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.cinemachine",
            "com.unity.inputsystem",
            "com.unity.test-framework",
            "com.unity.ugui", // uGUI + TextMeshPro (DialogueUI/InventoryUI/etc.)
        };
        Debug.Log("[NSSetup] Adding packages: " + string.Join(", ", toAdd));
        _addReq = Client.AddAndRemove(toAdd, null);
        EditorApplication.update += PollAdd;
    }

    private static void PollAdd()
    {
        if (_addReq == null || !_addReq.IsCompleted) return;
        EditorApplication.update -= PollAdd;

        if (_addReq.Status == StatusCode.Success)
        {
            Debug.Log("[NSSetup] PACKAGES_ADDED_OK");
            foreach (var p in _addReq.Result
                         .Where(p => !p.name.StartsWith("com.unity.modules."))
                         .OrderBy(p => p.name))
            {
                Debug.Log($"[NSSetup] resolved {p.name}@{p.version}");
            }
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("[NSSetup] PACKAGES_FAILED: " + (_addReq.Error?.message ?? "unknown error"));
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Creates a Universal Render Pipeline asset + Universal Renderer under Assets/Settings/
    /// and assigns it as the default and per-quality-level pipeline. Synchronous; safe with -quit.
    /// </summary>
    public static void SetupURP()
    {
        const string dir = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(dir))
            AssetDatabase.CreateFolder("Assets", "Settings");

        const string rendererPath = dir + "/NS_UniversalRenderer.asset";
        const string urpPath = dir + "/NS_URPAsset.asset";

        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, rendererPath);

        var urp = UniversalRenderPipelineAsset.Create(rendererData);
        AssetDatabase.CreateAsset(urp, urpPath);
        AssetDatabase.SaveAssets();

        GraphicsSettings.defaultRenderPipeline = urp;
        int levels = QualitySettings.names.Length;
        for (int i = 0; i < levels; i++)
        {
            QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
            QualitySettings.renderPipeline = urp;
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[NSSetup] URP_SETUP_OK -> " + urpPath);
        EditorApplication.Exit(0);
    }
}
#endif
