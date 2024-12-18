using System.Collections.Generic;
using System.Linq;
using Unity.Netcode.Editor.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Netcode.Editor
{
#if UNITY_EDITOR
    /// <summary>
    /// Specialized editor specific NetworkManager code
    /// </summary>
    public class NetworkManagerHelper : NetworkManager.INetworkManagerHelper
    {
        internal static NetworkManagerHelper Singleton;

        // This is primarily to handle IntegrationTest scenarios where more than 1 NetworkManager could exist
        private static Dictionary<NetworkManager, Transform> s_LastKnownNetworkManagerParents = new Dictionary<NetworkManager, Transform>();

        /// <summary>
        /// Initializes the singleton instance and registers for:
        /// Hierarchy changed notification: to notify the user when they nest a NetworkManager
        /// Play mode state change notification: to capture when entering or exiting play mode (currently only exiting)
        /// </summary>
        [InitializeOnLoadMethod]
        private static void InitializeOnload()
        {
            Singleton = new NetworkManagerHelper();
            NetworkManager.NetworkManagerHelper = Singleton;
            EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
            EditorApplication.hierarchyChanged -= EditorApplication_hierarchyChanged;

            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
            EditorApplication.hierarchyChanged += EditorApplication_hierarchyChanged;

            // Initialize default values for new NetworkManagers
            //
            // When the default prefab list is enabled, this will default
            // new NetworkManagers to using it.
            //
            // This will get run when new NetworkManagers are added, and
            // when the user presses the "reset" button on a NetworkManager
            // in the inspector.
            NetworkManager.OnNetworkManagerReset = manager =>
            {
                var settings = NetcodeForGameObjectsProjectSettings.instance;
                if (settings.GenerateDefaultNetworkPrefabs)
                {
                    manager.NetworkConfig = new NetworkConfig();
                    manager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { NetworkPrefabProcessor.GetOrCreateNetworkPrefabs(NetworkPrefabProcessor.DefaultNetworkPrefabsPath, out _, true) };
                }
            };
        }

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            switch (playModeStateChange)
            {
                case PlayModeStateChange.ExitingEditMode:
                    {
                        s_LastKnownNetworkManagerParents.Clear();
                        ScenesInBuildActiveSceneCheck();
                        EditorApplication.hierarchyChanged -= EditorApplication_hierarchyChanged;
                        break;
                    }
                case PlayModeStateChange.EnteredEditMode:
                    {
                        EditorApplication.hierarchyChanged += EditorApplication_hierarchyChanged;
                        break;
                    }
            }
        }

        /// <summary>
        /// Detects if a user is trying to enter into play mode when both conditions are true:
        /// - the currently active and open scene is not added to the scenes in build list
        /// - an instance of a NetworkManager with scene management enabled can be found
        /// If both conditions are met then the user is presented with a dialog box that
        /// provides the user with the option to add the scene to the scenes in build list
        /// before entering into play mode or the user can continue under those conditions.
        ///
        /// ** When scene management is enabled the user should treat all scenes that need to
        /// be synchronized using network scene management as if they were preparing for a build.
        /// Any scene that needs to be loaded at run time has to be included in the scenes in
        /// build list. **
        /// </summary>
        private static void ScenesInBuildActiveSceneCheck()
        {
#if UNITY_2023_1_OR_NEWER
            var networkManager = Object.FindFirstObjectByType<NetworkManager>();
#else
            var networkManager = Object.FindObjectOfType<NetworkManager>();
#endif
            if (!IsActiveSceneInBuildList() && networkManager != null)
            {
                if (networkManager.NetworkConfig != null && networkManager.NetworkConfig.EnableSceneManagement)
                {
                    if (EditorUtility.DisplayDialog("Add Scene to Scenes in Build", $"The current scene was not found in the scenes" +
                        $" in build and a {nameof(NetworkManager)} instance was found with scene management enabled! Clients will not be able " +
                        $"to synchronize to this scene unless it is added to the scenes in build list.\n\nWould you like to add it now?",
                        "Yes", "No - Continue"))
                    {
                        // Double check that the scene hasn't already been added to the scenes in build list (potential issue with Parrel Sync)
                        if (!IsActiveSceneInBuildList())
                        {
                            var scenesList = EditorBuildSettings.scenes.ToList();
                            scenesList.Add(new EditorBuildSettingsScene(SceneManager.GetActiveScene().path, true));
                            EditorBuildSettings.scenes = scenesList.ToArray();
                        }
                    }
                }
            }
        }

        private static bool IsActiveSceneInBuildList()
        {
            var scenesList = EditorBuildSettings.scenes.ToList();
            var activeScene = SceneManager.GetActiveScene();
            var activeSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(activeScene.path);
            var activeSceneHash = activeSceneAsset.GetHashCode();

            foreach (var scene in scenesList)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
                var sceneAssetHash = sceneAsset.GetHashCode();
                if (activeSceneHash == sceneAssetHash)
                {
                    return true;
                }
                if (sceneAsset.name == activeSceneAsset.name)
                {
                    if (scene.path != activeScene.path)
                    {
                        Debug.LogError($"Active scene {activeScene.name} with path ({activeScene.path}) is potentially a duplicate of " +
                            $"scene {sceneAsset.name} with a path of ({scene.path})! Excluding from automatically adding to the scenes in build list!");
                    }
                    else
                    {
                        Debug.LogError($"Active scene {activeScene.name} was found but the active scene asset hash value ({activeSceneHash}) was " +
                            $"not the same as the the one in the scenes in build list ({sceneAssetHash})! Excluding from automatically adding to the scenes in build list!");
                    }
                    // Exit as if it did find a perfect match
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Invoked only when the hierarchy changes
        /// </summary>
        private static void EditorApplication_hierarchyChanged()
        {
            if (Application.isPlaying)
            {
                EditorApplication.hierarchyChanged -= EditorApplication_hierarchyChanged;
                return;
            }

            var allNetworkManagers = Resources.FindObjectsOfTypeAll<NetworkManager>();
            foreach (var networkManager in allNetworkManagers)
            {
                if (!networkManager.NetworkManagerCheckForParent())
                {
                    Singleton.CheckAndNotifyUserNetworkObjectRemoved(networkManager);
                }
            }
        }

        /// <summary>
        /// Handles notifying users that they cannot add a NetworkObject component
        /// to a GameObject that also has a NetworkManager component. The NetworkObject
        /// will always be removed.
        /// GameObject + NetworkObject then NetworkManager = NetworkObject removed
        /// GameObject + NetworkManager then NetworkObject = NetworkObject removed
        /// Note: Since this is always invoked after <see cref="NetworkManagerCheckForParent"/>
        /// we do not need to check for parent when searching for a NetworkObject component
        /// </summary>
        public void CheckAndNotifyUserNetworkObjectRemoved(NetworkManager networkManager, bool editorTest = false)
        {
            // Check for any NetworkObject at the same gameObject relative layer

            if (!networkManager.gameObject.TryGetComponent<NetworkObject>(out var networkObject))
            {
                // if none is found, check to see if any children have a NetworkObject
                networkObject = networkManager.gameObject.GetComponentInChildren<NetworkObject>();
                if (networkObject == null)
                {
                    return;
                }
            }

            if (!EditorApplication.isUpdating)
            {
                Object.DestroyImmediate(networkObject);

                if (!EditorApplication.isPlaying && !editorTest)
                {
                    EditorUtility.DisplayDialog($"Removing {nameof(NetworkObject)}", NetworkManagerAndNetworkObjectNotAllowedMessage(), "OK");
                }
                else
                {
                    Debug.LogError(NetworkManagerAndNetworkObjectNotAllowedMessage());
                }
            }
        }

        public string NetworkManagerAndNetworkObjectNotAllowedMessage()
        {
            return $"A {nameof(GameObject)} cannot have both a {nameof(NetworkManager)} and {nameof(NetworkObject)} assigned to it or any children under it.";
        }

        /// <summary>
        /// Handles notifying the user, via display dialog window, that they have nested a NetworkManager.
        /// When in edit mode it provides the option to automatically fix the issue
        /// When in play mode it just notifies the user when entering play mode as well as when the user
        /// tries to start a network session while a NetworkManager is still nested.
        /// </summary>
        public bool NotifyUserOfNestedNetworkManager(NetworkManager networkManager, bool ignoreNetworkManagerCache = false, bool editorTest = false)
        {
            var gameObject = networkManager.gameObject;
            var transform = networkManager.transform;
            var isParented = transform.root != transform;

            var message = NetworkManager.GenerateNestedNetworkManagerMessage(transform);
            if (s_LastKnownNetworkManagerParents.ContainsKey(networkManager) && !ignoreNetworkManagerCache)
            {
                // If we have already notified the user, then don't notify them again
                if (s_LastKnownNetworkManagerParents[networkManager] == transform.root)
                {
                    return isParented;
                }
                else // If we are no longer a child, then we can remove ourself from this list
                if (transform.root == gameObject.transform)
                {
                    s_LastKnownNetworkManagerParents.Remove(networkManager);
                }
            }
            if (!EditorApplication.isUpdating && isParented)
            {
                if (!EditorApplication.isPlaying && !editorTest)
                {
                    message += $"Click 'Auto-Fix' to automatically remove it from {transform.root.gameObject.name} or 'Manual-Fix' to fix it yourself in the hierarchy view.";
                    if (EditorUtility.DisplayDialog("Invalid Nested NetworkManager", message, "Auto-Fix", "Manual-Fix"))
                    {
                        transform.parent = null;
                        isParented = false;
                    }
                }
                else
                {
                    Debug.LogError(message);
                }

                if (!s_LastKnownNetworkManagerParents.ContainsKey(networkManager) && isParented)
                {
                    s_LastKnownNetworkManagerParents.Add(networkManager, networkManager.transform.root);
                }
            }
            return isParented;
        }
    }
#endif
}
