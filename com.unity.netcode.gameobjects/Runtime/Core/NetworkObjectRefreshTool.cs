#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Netcode
{
    /// <summary>
    /// This is a helper tool to update all in-scene placed instances of a prefab that 
    /// originally did not have a NetworkObject component but one was added to the prefab
    /// later.
    /// </summary>
    internal class NetworkObjectRefreshTool
    {
        private static List<string> s_ScenesToUpdate = new List<string>();
        private static bool s_ProcessScenes;
        private static bool s_CloseScenes;

        internal static Action AllScenesProcessed;

        internal static NetworkObject PrefabNetworkObject;

        internal static void LogInfo(string msg, bool append = false)
        {
            if (!append)
            {
                s_Log.AppendLine(msg);
            }
            else
            {
                s_Log.Append(msg);
            }
        }

        internal static void FlushLog()
        {
            Debug.Log(s_Log.ToString());
            s_Log.Clear();
        }

        private static StringBuilder s_Log = new StringBuilder();

        internal static void ProcessScene(string scenePath, bool processScenes = true)
        {
            if (!s_ScenesToUpdate.Contains(scenePath))
            {
                if (s_ScenesToUpdate.Count == 0)
                {
                    EditorSceneManager.sceneOpened += EditorSceneManager_sceneOpened;
                    EditorSceneManager.sceneSaved += EditorSceneManager_sceneSaved;
                    s_Log.Clear();
                    LogInfo("NetworkObject Refresh Scenes to Process:");
                }
                LogInfo($"[{scenePath}]", true);
                s_ScenesToUpdate.Add(scenePath);
            }
            s_ProcessScenes = processScenes;
        }

        internal static void ProcessActiveScene()
        {
            FlushLog();
            var activeScene = SceneManager.GetActiveScene();
            if (s_ScenesToUpdate.Contains(activeScene.path) && s_ProcessScenes)
            {
                SceneOpened(activeScene);
            }
        }

        internal static void ProcessScenes()
        {
            if (s_ScenesToUpdate.Count != 0)
            {
                s_CloseScenes = true;
                var scenePath = s_ScenesToUpdate.First();
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }
            else
            {
                s_ProcessScenes = false;
                s_CloseScenes = false;
                EditorSceneManager.sceneSaved -= EditorSceneManager_sceneSaved;
                EditorSceneManager.sceneOpened -= EditorSceneManager_sceneOpened;
                AllScenesProcessed?.Invoke();
                FlushLog();
            }
        }

        private static void FinishedProcessingScene(Scene scene, bool refreshed = false)
        {
            if (s_ScenesToUpdate.Contains(scene.path))
            {
                // Provide a log of all scenes that were modified to the user
                if (refreshed)
                {
                    LogInfo($"Refreshed and saved updates to scene: {scene.name}");
                }
                s_ScenesToUpdate.Remove(scene.path);

                if (scene != SceneManager.GetActiveScene())
                {
                    EditorSceneManager.CloseScene(scene, s_CloseScenes);
                }
                ProcessScenes();
            }
        }

        private static void EditorSceneManager_sceneSaved(Scene scene)
        {
            FinishedProcessingScene(scene, true);
        }

        private static void SceneOpened(Scene scene)
        {
            LogInfo($"Processing scene {scene.name}:");
            if (s_ScenesToUpdate.Contains(scene.path))
            {
                if (s_ProcessScenes)
                {
                    var prefabInstances = PrefabUtility.FindAllInstancesOfPrefab(PrefabNetworkObject.gameObject);

                    if (prefabInstances.Length > 0)
                    {
                        var instancesSceneLoadedSpecific = prefabInstances.Where((c) => c.scene == scene).ToList();

                        if (instancesSceneLoadedSpecific.Count > 0)
                        {
                            foreach (var prefabInstance in instancesSceneLoadedSpecific)
                            {
                                prefabInstance.GetComponent<NetworkObject>().OnValidate();
                            }

                            if (!EditorSceneManager.MarkSceneDirty(scene))
                            {
                                LogInfo($"Scene {scene.name} did not get marked as dirty!");
                                FinishedProcessingScene(scene);
                            }
                            else
                            {
                                LogInfo($"Changes detected and applied!");
                                EditorSceneManager.SaveScene(scene);
                            }
                            return;
                        }
                    }
                }

                LogInfo($"No changes required.");
                FinishedProcessingScene(scene);
            }
        }

        private static void EditorSceneManager_sceneOpened(Scene scene, OpenSceneMode mode)
        {
            SceneOpened(scene);
        }
    }
}
#endif // UNITY_EDITOR
