using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The boot scene's main menu (IMGUI, like the other slice glue — no Canvas/TMP deps).
/// New Game loads the gameplay scene; Continue appears only when the configured save slot
/// exists (SaveSystem); Quit exits the player. GameManager starts in
/// <see cref="GameState.MainMenu"/> and the gameplay scene's bootstrap flips it to
/// Exploring after the load. Composition-root glue (NorthStar.Game).
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Tooltip("Scene loaded by New Game / Continue.")]
    [SerializeField] private string _gameplayScene = "SCN_VerticalSlice";

    [Tooltip("Save slot Continue checks for (the slice NPC saves to this slot).")]
    [SerializeField] private string _continueSlot = "smoke";

    private void OnGUI()
    {
        float w = 300f, h = 260f;
        GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h), GUI.skin.box);

        var title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        GUILayout.Label("NORTH STAR", title, GUILayout.Height(56f));
        GUILayout.Space(10f);

        if (GUILayout.Button("New Game", GUILayout.Height(36f)))
            StartGame();

        if (SaveSystem.SaveExists(_continueSlot) && GUILayout.Button("Continue", GUILayout.Height(36f)))
            StartGame();

        GUILayout.Space(6f);
        if (GUILayout.Button("Quit", GUILayout.Height(30f)))
            Application.Quit();

        GUILayout.EndArea();
    }

    private void StartGame()
    {
        SceneManager.LoadScene(_gameplayScene, LoadSceneMode.Single);
    }
}
