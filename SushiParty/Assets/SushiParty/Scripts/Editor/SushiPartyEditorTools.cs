using System.Collections.Generic;
using System.Text;
using SushiParty.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SushiParty.EditorTools
{
    public static class SushiPartyEditorTools
    {
        private const string SceneFolder = "Assets/SushiParty/Scenes";
        private const string BootScenePath = SceneFolder + "/Boot.unity";
        private const string MenuScenePath = SceneFolder + "/MainMenu.unity";
        private const string BoardScenePath = SceneFolder + "/" + MinigameFlow.BoardSceneName + ".unity";
        private const string BoardRootName = "Board Mode";
        private const float CameraHeight = 31f;
        private const float CameraBack = 22f;
        private const float CameraPitch = 58f;
        private const float CameraFieldOfView = 60f;
        private const float LightHeight = 12f;
        private const float LightPitch = 50f;
        private const float LightYaw = -30f;
        private const float LightIntensity = 1.9f;
        private const float LightShadowStrength = 0.75f;
        private static readonly Color NightWater = new Color(0.05f, 0.06f, 0.09f);
        private static readonly Color LampWhite = new Color(1f, 0.98f, 0.93f);

        public static void SyncScenesToBuildSettings()
        {
            StringBuilder missing = new StringBuilder();

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(MenuScenePath, true),
            };

            if (System.IO.File.Exists(BoardScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(BoardScenePath, true));
            }
            else
            {
                missing.AppendLine($"  Board — no scene at '{BoardScenePath}'");
            }

            foreach (MinigameDefinition definition in MinigameLibrary.All)
            {
                string path = FindScenePath(definition.SceneName);
                if (string.IsNullOrEmpty(path))
                {
                    missing.AppendLine($"  {definition.DisplayName} — no scene named '{definition.SceneName}.unity'");
                    continue;
                }

                scenes.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (!scenes.Exists(s => s.path == existing.path))
                {
                    scenes.Add(new EditorBuildSettingsScene(existing.path, false));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();

            if (missing.Length > 0)
            {
                Debug.LogWarning($"[SushiParty] Build settings synced, but some scenes are absent:\n{missing}");
                return;
            }

            Debug.Log($"[SushiParty] Build settings synced — {scenes.Count} scenes, Boot first, Board third.");
        }

        public static void ValidateFromCommandLine()
        {
            int problems = Validate(out string report);
            Debug.Log($"[SushiParty] Validation report:\n{report}");
            EditorApplication.Exit(problems == 0 ? 0 : 1);
        }

        public static void CreateBoardSceneFromCommandLine()
        {
            bool built = BuildBoardScene(out string report);
            Debug.Log($"[SushiParty] Board scene report:\n{report}");

            if (built)
            {
                SyncScenesToBuildSettings();
            }

            AssetDatabase.SaveAssets();
            EditorApplication.Exit(built ? 0 : 1);
        }

        private static bool BuildBoardScene(out string report)
        {
            StringBuilder sb = new StringBuilder();

            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                report = $"FAIL  {SceneFolder}: folder does not exist\n";
                return false;
            }

            bool rebuilt = System.IO.File.Exists(BoardScenePath);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateBoardCamera(sb);
            CreateBoardLight(sb);

            GameObject root = new GameObject(BoardRootName, typeof(Board.BoardController));
            sb.AppendLine($"ok    {root.name} — {nameof(Board.BoardController)}");

            if (!EditorSceneManager.SaveScene(scene, BoardScenePath))
            {
                sb.AppendLine($"FAIL  {BoardScenePath}: SaveScene refused to write");
                report = sb.ToString();
                return false;
            }

            AssetDatabase.ImportAsset(BoardScenePath);

            sb.AppendLine($"ok    {BoardScenePath} — {(rebuilt ? "rebuilt in place" : "created")}");
            report = sb.ToString();
            return true;
        }

        private static void CreateBoardCamera(StringBuilder sb)
        {
            GameObject host = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener))
            {
                tag = "MainCamera",
            };

            host.transform.SetPositionAndRotation(
                new Vector3(0f, CameraHeight, -CameraBack),
                Quaternion.Euler(CameraPitch, 0f, 0f));

            Camera camera = host.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = NightWater;
            camera.fieldOfView = CameraFieldOfView;

            sb.AppendLine($"ok    {host.name} — solid colour, {CameraFieldOfView}° over the whole ring");

            if (!AddUrpData(host, "UniversalAdditionalCameraData"))
            {
                sb.AppendLine("warn  no URP camera data — Universal RP is not in this project");
            }
        }

        private static void CreateBoardLight(StringBuilder sb)
        {
            GameObject host = new GameObject("Directional Light", typeof(Light));

            host.transform.SetPositionAndRotation(
                new Vector3(0f, LightHeight, 0f),
                Quaternion.Euler(LightPitch, LightYaw, 0f));

            Light light = host.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = LampWhite;
            light.intensity = LightIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = LightShadowStrength;

            sb.AppendLine($"ok    {host.name} — directional, soft shadows");

            if (!AddUrpData(host, "UniversalAdditionalLightData"))
            {
                sb.AppendLine("warn  no URP light data — Universal RP is not in this project");
            }
        }

        private static bool AddUrpData(GameObject host, string typeName)
        {
            System.Type type = System.Type.GetType(
                $"UnityEngine.Rendering.Universal.{typeName}, Unity.RenderPipelines.Universal.Runtime");

            if (type == null)
            {
                return false;
            }

            host.AddComponent(type);
            return true;
        }

        private static int Validate(out string report)
        {
            StringBuilder sb = new StringBuilder();
            int problems = 0;

            problems += CheckPlainScene(BootScenePath, typeof(SushiPartyRoot), sb);
            problems += CheckPlainScene(MenuScenePath, typeof(Menu.MainMenuController), sb);

            problems += CheckPlainScene(BoardScenePath, typeof(Board.BoardController), sb, requireCamera: true);

            foreach (MinigameDefinition definition in MinigameLibrary.All)
            {
                string path = FindScenePath(definition.SceneName);
                if (string.IsNullOrEmpty(path))
                {
                    sb.AppendLine($"FAIL  {definition.DisplayName}: no scene '{definition.SceneName}.unity'");
                    problems++;
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                problems += CountMissingScripts(scene, sb);

                MinigameController controller = Object.FindFirstObjectByType<MinigameController>();
                if (controller == null)
                {
                    sb.AppendLine($"FAIL  {definition.DisplayName}: scene has no MinigameController");
                    problems++;
                    continue;
                }

                if (controller.Id != definition.Id)
                {
                    sb.AppendLine(
                        $"FAIL  {definition.DisplayName}: controller reports {controller.Id}, expected {definition.Id}");
                    problems++;
                    continue;
                }

                if (Object.FindFirstObjectByType<Camera>() == null)
                {
                    sb.AppendLine($"FAIL  {definition.DisplayName}: scene has no Camera");
                    problems++;
                    continue;
                }

                string kind = definition.Implemented ? controller.GetType().Name : "placeholder";
                int wired = ReportObjectReferences(controller, sb);
                sb.AppendLine($"ok    {definition.DisplayName} — {kind}{(wired > 0 ? $", {wired} scene reference(s) wired" : string.Empty)}");
            }

            report = sb.ToString();
            return problems;
        }

        private static int CheckPlainScene(
            string path,
            System.Type required,
            StringBuilder sb,
            bool requireCamera = false)
        {
            if (!System.IO.File.Exists(path))
            {
                sb.AppendLine($"FAIL  {path}: missing");
                return 1;
            }

            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int problems = CountMissingScripts(scene, sb);

            if (Object.FindFirstObjectByType(required) == null)
            {
                sb.AppendLine($"FAIL  {path}: no {required.Name} in scene");
                problems++;
            }

            if (requireCamera && Object.FindFirstObjectByType<Camera>() == null)
            {
                sb.AppendLine($"FAIL  {path}: no Camera in scene");
                problems++;
            }

            if (problems == 0)
            {
                sb.AppendLine($"ok    {path} — {required.Name}{(requireCamera ? ", camera" : string.Empty)}");
            }

            return problems;
        }

        private static int CountMissingScripts(Scene scene, StringBuilder sb)
        {
            int missing = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null)
                    {
                        missing++;
                    }
                }
            }

            if (missing > 0)
            {
                sb.AppendLine($"FAIL  {scene.name}: {missing} missing script reference(s)");
            }

            return missing;
        }

        private static int ReportObjectReferences(MinigameController controller, StringBuilder sb)
        {
            int wired = 0;
            SerializedObject serialized = new SerializedObject(controller);
            SerializedProperty property = serialized.GetIterator();

            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference
                    || property.name == "m_Script")
                {
                    continue;
                }

                if (property.objectReferenceValue != null)
                {
                    wired++;
                    continue;
                }

                sb.AppendLine($"warn  {controller.GetType().Name}.{property.name} is unassigned");
            }

            return wired;
        }

        private static string FindScenePath(string sceneName)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:Scene {sceneName}", new[] { SceneFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return path;
                }
            }

            return null;
        }
    }
}
