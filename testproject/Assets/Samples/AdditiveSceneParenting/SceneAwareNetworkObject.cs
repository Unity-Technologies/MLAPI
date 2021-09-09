
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// A Proof of Concept - ProtoType "Scene Aware NetworkObject"
/// !!This cannot be used with in scene placed NetworkObjects!!
/// </summary>
public class SceneAwareNetworkObject : NetworkBehaviour
{
    private int m_OriginalScene;
    private NetworkVariable<int> m_CurrentScene = new NetworkVariable<int>();

    /// <summary>
    /// Client and Server Side:
    /// Non-Server Clients are notified when the associated NetworkObject's GameObject's
    /// current scene index has changed.
    /// The server tracks the original scene this GameObjet was in so it can, if needed,
    /// send the GameObject back to its original scene.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            m_CurrentScene.OnValueChanged = OnValueChangedDelegate;
        }
        else
        {
            m_OriginalScene = gameObject.scene.buildIndex;
        }

        base.OnNetworkSpawn();
    }

    /// <summary>
    /// Client Side:
    /// When the current scene index changes, all clients should reflect the change for this
    /// SceneAwareNetworkObject
    /// </summary>
    private void OnValueChangedDelegate(int previousValue, int newValue)
    {
        if (NetworkManager != null && NetworkManager.IsListening && !IsServer)
        {
            if (!NetworkObject.IsSceneObject.Value)
            {
                var targetScene = SceneManager.GetSceneByBuildIndex(newValue);
                if (gameObject.scene != targetScene)
                {
                    SceneManager.MoveGameObjectToScene(gameObject, targetScene);
                }
            }
            else
            {
                Debug.LogError($"{nameof(SceneAwareNetworkObject)}s cannot be in-scene placed NetworkObjects!");
            }
        }
    }

    /// <summary>
    /// Server Side:
    /// Spawns the associated NetworkObject and then moves it into a specific scene
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="destroyWithScene"></param>
    public void SpawnInScene(Scene scene, bool destroyWithScene = false)
    {
        if (NetworkManager != null && NetworkManager.IsListening && IsServer)
        {
            NetworkObject.Spawn(destroyWithScene);
            MoveToScene(scene);
            m_OriginalScene = scene.buildIndex;
        }
        else
        {
            Debug.LogError($"Only the server can spawn a {nameof(SceneAwareNetworkObject)} into another scene!");
        }
    }

    /// <summary>
    /// ServerSide:
    /// This will attempt to move the SceneAwareNetworkObject back to the original scene it was spawned in.
    /// </summary>
    /// <returns></returns>
    protected bool TryMoveBackToOriginalScene()
    {
        if (NetworkManager != null && NetworkManager.IsListening && IsServer)
        {
            var originalScene = SceneManager.GetSceneByBuildIndex(m_OriginalScene);
            if (originalScene.IsValid() && originalScene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(gameObject, originalScene);
                m_CurrentScene.Value = m_OriginalScene;
                return true;
            }
        }
        else
        {
            Debug.LogError($"Only the server can move a {nameof(SceneAwareNetworkObject)} back to its original scene!");
        }
        return false;
    }

    /// <summary>
    /// Server Side:
    /// This will move the SceneAwareNetworkObject into the scene specified (if it is valid and loaded)
    /// </summary>
    /// <param name="scene"></param>
    public void MoveToScene(Scene scene)
    {
        if (IsServer)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(gameObject, scene);
                m_CurrentScene.Value = scene.buildIndex;
            }
        }
        else
        {
            Debug.LogError($"Only the server can move a {nameof(SceneAwareNetworkObject)} to another scene!");
        }
    }
}
