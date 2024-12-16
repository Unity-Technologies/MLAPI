
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using Unity.Netcode.Editor;
using UnityEditor;

/// <summary>
/// The custom editor for the <see cref="SceneBootstrapLoaderEditor"/> component.
/// </summary>
[CustomEditor(typeof(SceneBootstrapLoader), true)]
[CanEditMultipleObjects]
public class SceneBootstrapLoaderEditor : NetcodeEditorBase<SceneBootstrapLoader>
{
    private SerializedProperty m_ServerSceneDefines;
    private SerializedProperty m_ClientSceneDefines;

    public override void OnEnable()
    {
        m_ServerSceneDefines = serializedObject.FindProperty(nameof(SceneBootstrapLoader.ServerSceneDefines));
        m_ClientSceneDefines = serializedObject.FindProperty(nameof(SceneBootstrapLoader.ClientSceneDefines));
        base.OnEnable();
    }

    private void DisplaySceneBootstrapLoaderProperties()
    {
        var sceneBootstrapLoader = target as SceneBootstrapLoader;
        var networkManager = sceneBootstrapLoader.GetComponent<NetworkManager>();
        if (networkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.ClientServer)
        {
            EditorGUILayout.PropertyField(m_ServerSceneDefines);
        }
        EditorGUILayout.PropertyField(m_ClientSceneDefines);
    }

    public override void OnInspectorGUI()
    {
        var sceneBootstrapLoader = target as SceneBootstrapLoader;
        void SetExpanded(bool expanded) { sceneBootstrapLoader.SceneBootstrapLoaderExpanded = expanded; };
        DrawFoldOutGroup<NetworkManagerBootstrapper>(sceneBootstrapLoader.GetType(), DisplaySceneBootstrapLoaderProperties, sceneBootstrapLoader.SceneBootstrapLoaderExpanded, SetExpanded);
        base.OnInspectorGUI();
    }
}

#endif

/// <summary>
/// Example of how to control scene loading using a <see cref="NetworkSceneManager"/> additive client
/// synchronization mode and server-side scene validation approach.
/// </summary>
/// <remarks>
/// This component should be added to the same GameObject that the <see cref="NetworkManager"/> component
/// is attached to (for this example we extended <see cref="NetworkManager"/> to <see cref="NetworkManagerBootstrapper"/>).
/// </remarks>
[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(SceneBootstrapLoader))]
public class SceneBootstrapLoader : MonoBehaviour
{
#if UNITY_EDITOR
    // Inspector view expand/collapse settings for this derived child class
    [HideInInspector]
    public bool SceneBootstrapLoaderExpanded;

    [Tooltip("The main menu or out of session scene to load.")]
    public SceneAsset MainMenuSceneAsset;

    /// <summary>
    /// Converts SceneAssets to scene names
    /// </summary>
    private void OnValidate()
    {
        if (MainMenuSceneAsset != null)
        {
            m_MainMenuScene = MainMenuSceneAsset.name;
        }
        var networkManager = GetComponent<NetworkManager>();
        if (networkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.ClientServer)
        {
            ServerSceneDefines.Validate();
        }
        ClientSceneDefines.Validate();
    }
#endif

    [Tooltip("Defines how to handle scenes for the server instance.")]
    public ServerSceneDefine ServerSceneDefines;
    [Tooltip("Defines how to handle scenes for the client instance.")]
    public SceneDefine ClientSceneDefines;

    private NetworkManagerBootstrapper m_NetworkManager;
    private string m_SceneCurrentlyLoading;
    private string m_SceneJustLoaded;

    [HideInInspector]
    [SerializeField]
    private string m_MainMenuScene;

    private void Awake()
    {
        m_NetworkManager = GetComponent<NetworkManagerBootstrapper>();
    }

    /// <summary>
    /// Should be invoked by bootstrap when first starting the applicaiton and should be loaded upon exiting
    /// a session and shutting down the <see cref="NetworkManagerBootstrapper"/>.
    /// </summary>
    public void LoadMainMenu()
    {
        if (!m_NetworkManager.IsListening)
        {
            SceneManager.LoadScene(m_MainMenuScene, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning($"Trying to load main scene {m_MainMenuScene} while {nameof(NetworkManagerBootstrapper)} is still running!");
        }
    }

    public enum StartAsTypes
    {
        Server,
        Host,
        Client
    }

    /// <summary>
    /// Invoked by the <see cref="NetworkManagerBootstrapper"/> when
    /// starting a client or server.
    /// </summary>
    /// <param name="startAsServer"></param>
    public void StartSession(StartAsTypes startAsType)
    {
        StartCoroutine(PreSceneLoading(startAsType));
    }

    /// <summary>
    /// Both clients and the server invoke this when they shutdown.
    /// </summary>
    private void OnNetworkManagerShutdown(bool wasHost)
    {
        LoadMainMenu();
    }

    #region SCENE PRE & POST START LOADING METHODS



    private IEnumerator PreSceneLoading(StartAsTypes startAsType)
    {
        var sceneDefines = startAsType == StartAsTypes.Client ? ClientSceneDefines : ServerSceneDefines;
        SceneManager.sceneLoaded += SceneLoaded;

        // Unloads any currently loaded scenes and becomes the default active scene.
        // The default active scene ** has to be shared ** because it is where GameObjects are
        // instantiated (which includes where network prefabs are instantiated when spawned dynamically)
        yield return HandleSceneLoading(sceneDefines.DefaultActiveScene, LoadSceneMode.Single);

        // Now load our local server or clien scenes additively
        foreach (var sceneName in sceneDefines.LocalScenes)
        {
            yield return HandleSceneLoading(sceneName, LoadSceneMode.Additive);
        }

        // When running as a host, we load the client scenes too
        if (startAsType == StartAsTypes.Host)
        {
            foreach (var sceneName in ClientSceneDefines.LocalScenes)
            {
                yield return HandleSceneLoading(sceneName, LoadSceneMode.Additive);
            }
        }

        SceneManager.sceneLoaded -= SceneLoaded;

        // Now start the NetworkManager (server or client)
        if (startAsType != StartAsTypes.Client && m_NetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.ClientServer)
        {
            // Server needs to do some additional congiruations to ignore the local scene loaded and
            // will load any additional shared and synchronized scenes via the NetworkSceneManager.
            m_NetworkManager.OnServerStarted += OnServerStarted;
            m_NetworkManager.OnServerStopped += OnNetworkManagerShutdown;
            if (startAsType == StartAsTypes.Server)
            {
                m_NetworkManager.StartServer();
            }
            else
            {
                m_NetworkManager.StartHost();
            }
        }
        else
        {
            m_NetworkManager.OnClientStopped += OnNetworkManagerShutdown;

            // Distributed authority needs to start using the service and needs to know when the client becomes
            // the session owner.
            if (m_NetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.DistributedAuthority)
            {
                m_NetworkManager.OnSessionOwnerPromoted += OnSessionOwnerPromoted;
                m_NetworkManager.StartOrConnectToDistributedAuthoritySession();
            }
            else
            {
                m_NetworkManager.StartClient();
            }
        }
    }

    /// <summary>
    /// When the session owner is promoted, it needs to configure itself for the additive client synchronization
    /// and handle scene validations.
    /// </summary>
    /// <param name="sessionOwnerPromoted"></param>
    private void OnSessionOwnerPromoted(ulong sessionOwnerPromoted)
    {
        if (sessionOwnerPromoted == m_NetworkManager.LocalClientId)
        {
            // When we set the client synchronization mode to additive, the session owner will include this setting
            // setting when synchronizing a newly joining client and the client will use any already loaded scenes 
            // that the session owner determines should be synchronized. If a scene that is being synchronized is not
            // yet loaded, then the client will load that scene.
            m_NetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);

            // Add a callback to control which scene the session owner synchronizes with clients
            m_NetworkManager.SceneManager.VerifySceneBeforeLoading = SessionOwnerVerifySceneShouldBeSynchronized;
        }
    }

    /// <summary>
    /// Handles scene loading via <see cref="SceneManager"/> or <see cref="NetworkSceneManager"/> depending upon
    /// whether the <see cref="NetworkManager"/> is started or not.
    /// </summary>
    private IEnumerator HandleSceneLoading(string sceneName, LoadSceneMode loadSceneMode)
    {
        m_SceneJustLoaded = string.Empty;
        m_SceneCurrentlyLoading = sceneName;

        // Server will be the only NetworkManager instance where this is true
        if (m_NetworkManager.IsListening)
        {

            var loadingStatus = m_NetworkManager.SceneManager.LoadScene(sceneName, loadSceneMode);
            if (loadingStatus != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"{nameof(NetworkSceneManager)} attempted to load scene {sceneName} but returned a status of {loadingStatus}!");
                yield break;
            }
        }
        else // Client and Server will both use this to preload their local scenes
        {
            SceneManager.LoadScene(m_SceneCurrentlyLoading, loadSceneMode);
        }
        while (m_SceneJustLoaded != m_SceneCurrentlyLoading)
        {
            yield return null;
        }
    }

    private void SceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        m_SceneJustLoaded = scene.name;
    }
    #endregion

    #region SERVER POST START CONFIGURATION AND ADDITIONAL SHARED SCENE LOADING
    /// <summary>
    /// Starts the <see cref="NetworkManagerBootstrapper"/> as a server.
    /// </summary>
    private void OnServerStarted()
    {
        m_NetworkManager.OnServerStarted -= OnServerStarted;

        // When we set the client synchronization mode to additive, the server will include this setting
        // when synchronizing a client and the client will use any already loaded scenes that the server
        // wants to synchronize. If a scene that is being synchronized is not yet loaded, then the client
        // will load that scene.
        m_NetworkManager.SceneManager.SetClientSynchronizationMode(LoadSceneMode.Additive);

        // Add a callback to control which scene the server synchronizes with clients
        m_NetworkManager.SceneManager.VerifySceneBeforeLoading = ServerVerifySceneShouldBeSynchronized;

        // If we have any additional shared scenes we want to load, then load them via NetworkSceneManager
        if (ServerSceneDefines.SharedScenes.Count > 0)
        {
            StartCoroutine(ServerLoadAdditionalSharedScenes());
        }
    }

    /// <summary>
    /// When a client is first synchronized, the server will determine what scenes it should synchronize with the
    /// client. This callback is invoked for every scene currently loaded and if it returns false then it will not
    /// attempt to synchronize the scene with the client being synchronized.
    /// </summary>
    private bool ServerVerifySceneShouldBeSynchronized(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
    {
        return !ServerSceneDefines.LocalScenes.Contains(sceneName);
    }

    /// <summary>
    /// When a client is first synchronized, the session owner will determine what scenes it should synchronize with the
    /// client. This callback is invoked for every scene currently loaded and if it returns false then it will not
    /// attempt to synchronize the scene with the client being synchronized.
    /// </summary>
    /// <remarks>
    /// We use the <see cref="ClientSceneDefines"/> for distributed authority to just exclude scenes that are already preloaded.
    /// This is optional and semi-redundant since in a distributed network topology state is mirrored across all clients and
    /// having locally loaded scenes not synchronized just limits where spawned objects can be migrated.
    /// This is just for example purposes in the event you might need platform specific scenes loaded on a per client basis and/or
    /// have some other project specific need to have locally loaded scenes that are not synchronized between all connected clients.
    /// The recommended design for a distributed authority network topology is to just synchronize all scenes if possible.
    /// </remarks>
    private bool SessionOwnerVerifySceneShouldBeSynchronized(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
    {
        return !ClientSceneDefines.LocalScenes.Contains(sceneName);
    }


    private IEnumerator ServerLoadAdditionalSharedScenes()
    {
        m_NetworkManager.SceneManager.OnLoadEventCompleted += Server_OnLoadEventCompleted;

        foreach (var sharedScene in ServerSceneDefines.SharedScenes)
        {
            yield return HandleSceneLoading(sharedScene, LoadSceneMode.Additive);
        }
    }

    private void Server_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        m_SceneJustLoaded = sceneName;
    }
    #endregion
}

/// <summary>
/// The server scene defines included in (i.e. <see cref="SceneDefine"/>) and this provides
/// you with the ability to have additional additively loaded scenes that the server will
/// synchronize with clients.
/// </summary>
[Serializable]
public class ServerSceneDefine : SceneDefine
{
    [HideInInspector]
    public List<string> SharedScenes;

#if UNITY_EDITOR
    [Tooltip("These scenes will be loaded additively upon starting a session and will be synchronized.")]
    public List<SceneAsset> SharedSceneAssets;
    protected override void OnValidate()
    {
        SharedScenes = new List<string>();
        foreach (var sharedSceneAsset in SharedSceneAssets)
        {
            if (sharedSceneAsset != null)
            {
                SharedScenes.Add(sharedSceneAsset.name);
            }
        }
        base.OnValidate();
    }
#endif
}

/// <summary>
/// The base client and server scenes loaded prior to starting:
/// <see cref="DefaultActiveScene"/>: Must be synchonized/shared and is the default active scene.
/// <see cref="LocalScenes"/>: Any client or server specific scenes you don't want synchronized, but
/// want either the clients or server to load prior to starting a session.
/// </summary>
[Serializable]
public class SceneDefine
{
    [HideInInspector]
    public string DefaultActiveScene;
    [HideInInspector]
    public List<string> LocalScenes;

#if UNITY_EDITOR
    [Tooltip("This scene is synchronized and the default active scene loaded as LoadSceneMode.Single")]
    public SceneAsset DefaultActiveSceneAsset;
    [Tooltip("These scenes will be loaded additively and will not be synchronized.")]
    public List<SceneAsset> LocalSceneAssets;

    /// <summary>
    /// Invoked by <see cref="SceneBootstrapLoader"/>.
    /// </summary>
    /// <remarks>
    /// This is not the same as the Unity MonoBehaviour's OnValidate
    /// </remarks>
    protected virtual void OnValidate()
    {
        DefaultActiveScene = string.Empty;
        if (DefaultActiveSceneAsset)
        {
            DefaultActiveScene = DefaultActiveSceneAsset.name;
        }
        LocalScenes = new List<string>();
        foreach (var localSceneAsset in LocalSceneAssets)
        {
            if (localSceneAsset != null)
            {
                LocalScenes.Add(localSceneAsset.name);
            }
        }
    }

    public void Validate()
    {
        OnValidate();
    }
#endif
}
