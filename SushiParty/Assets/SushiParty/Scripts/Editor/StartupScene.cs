using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace SushiParty.EditorTools
{
    [InitializeOnLoad]
    public static class StartupScene
    {
        private const string ScenePath = "Assets/SushiParty/Scenes/MainMenu.unity";
        private const string CheckedKey = "SushiParty.StartupSceneChecked";

        static StartupScene()
        {
            EditorApplication.delayCall += OpenIfNothingRestored;
        }

        private static void OpenIfNothingRestored()
        {
            if (SessionState.GetBool(CheckedKey, false))
            {
                return;
            }

            SessionState.SetBool(CheckedKey, true);

            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                return;
            }

            // A restored scene setup gives the active scene a path; an untitled one means there
            // was nothing to restore, which is the fresh-open case this exists for.
            if (!string.IsNullOrEmpty(SceneManager.GetActiveScene().path) || !File.Exists(ScenePath))
            {
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
