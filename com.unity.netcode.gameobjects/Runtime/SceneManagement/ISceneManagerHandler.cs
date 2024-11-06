using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Unity.Netcode
{
    /// <summary>
    /// Used to override the LoadSceneAsync and UnloadSceneAsync methods called
    /// within the NetworkSceneManager.
    /// </summary>
    public interface ISceneManagerHandler
    {
        AsyncOperationHandle<SceneInstance> LoadSceneAsync(string sceneAssetKey, LoadSceneMode loadSceneMode, SceneEventProgress sceneEventProgress);

        AsyncOperationHandle<SceneInstance> UnloadSceneAsync(NetworkSceneManager.SceneData scene, SceneEventProgress sceneEventProgress);

        void PopulateLoadedScenes(ref Dictionary<int, NetworkSceneManager.SceneData> scenesLoaded, NetworkManager networkManager = null);
        Scene GetSceneFromLoadedScenes(string sceneName, NetworkManager networkManager = null);

        bool DoesSceneHaveUnassignedEntry(string sceneName, NetworkManager networkManager = null);

        void StopTrackingScene(int handle, string name, NetworkManager networkManager = null);

        void StartTrackingScene(Scene scene, bool assigned, NetworkManager networkManager = null);

        void ClearSceneTracking(NetworkManager networkManager = null);

        void UnloadUnassignedScenes(NetworkManager networkManager = null);

        void MoveObjectsFromSceneToDontDestroyOnLoad(ref NetworkManager networkManager, Scene scene);

        void SetClientSynchronizationMode(ref NetworkManager networkManager, LoadSceneMode mode);

        bool ClientShouldPassThrough(string sceneName, bool isPrimaryScene, LoadSceneMode clientSynchronizationMode, NetworkManager networkManager);

        bool IsIntegrationTest();
    }
}
