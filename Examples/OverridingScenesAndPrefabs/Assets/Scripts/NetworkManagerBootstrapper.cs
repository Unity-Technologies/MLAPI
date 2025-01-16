#if !DEDICATED_SERVER || (DEDICATED_SERVER && !UNITY_EDITOR)
using System;
#endif
using System.Collections.Generic;
using System.Linq;
#if !DEDICATED_SERVER
using System.Threading.Tasks;
#endif
using Unity.Netcode;
#if !DEDICATED_SERVER
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
#else
using Unity.Netcode.Transports.UTP;
#endif
using UnityEngine;
#if !DEDICATED_SERVER
using SessionState = Unity.Services.Multiplayer.SessionState;
#endif

#region NetworkManagerBootstrapperEditor
#if UNITY_EDITOR
using Unity.Netcode.Editor;
using UnityEditor;

/// <summary>
/// The custom editor for the <see cref="NetworkManagerBootstrapper"/> component.
/// </summary>
[CustomEditor(typeof(NetworkManagerBootstrapper), true)]
[CanEditMultipleObjects]
public class NetworkManagerBootstrapperEditor : NetworkManagerEditor
{
    private SerializedProperty m_TargetFrameRate;
    private SerializedProperty m_EnableVSync;

    public override void OnEnable()
    {
        m_TargetFrameRate = serializedObject.FindProperty(nameof(NetworkManagerBootstrapper.TargetFrameRate));
        m_EnableVSync = serializedObject.FindProperty(nameof(NetworkManagerBootstrapper.EnableVSync));
        base.OnEnable();
    }

    private void DisplayNetworkManagerBootstrapperProperties()
    {
        EditorGUILayout.PropertyField(m_TargetFrameRate);
        EditorGUILayout.PropertyField(m_EnableVSync);
    }

    public override void OnInspectorGUI()
    {
        var extendedNetworkManager = target as NetworkManagerBootstrapper;
        void SetExpanded(bool expanded) { extendedNetworkManager.NetworkManagerBootstrapperExpanded = expanded; };
        DrawFoldOutGroup<NetworkManagerBootstrapper>(extendedNetworkManager.GetType(), DisplayNetworkManagerBootstrapperProperties, extendedNetworkManager.NetworkManagerBootstrapperExpanded, SetExpanded);
        base.OnInspectorGUI();
    }
}
#endif
#endregion

/// <summary>
/// An extended NetworkManager to handle the bootstrap loading process specific to a client-server
/// topology where one might want to have local server-side scenes, local client-side scenes, and shared (synchronized) scenes.
/// <see cref="SceneBootstrapLoader"/>
/// </summary>
public class NetworkManagerBootstrapper : NetworkManager
{
    #region Validation
#if UNITY_EDITOR
    // Inspector view expand/collapse settings for this derived child class
    [HideInInspector]
    public bool NetworkManagerBootstrapperExpanded;
    protected override void OnValidateComponent()
    {
        m_OriginalVSyncCount = QualitySettings.vSyncCount;
        m_ServicesRegistered = CloudProjectSettings.organizationName != string.Empty && CloudProjectSettings.organizationId != string.Empty;
        base.OnValidateComponent();
    }
#endif
#endregion

    #region Properties
    public static NetworkManagerBootstrapper Instance;

    public int TargetFrameRate = 100;
    public bool EnableVSync = false;

    [HideInInspector]
    [SerializeField]
    private int m_OriginalVSyncCount;

    /// <summary>
    /// Example of how to control scene loading server local, client local, or shared.
    /// Server local: nothing is synchronized with clients.
    /// Client local: nothing is synchronized with the server.
    /// Shared: Is synchronized with clients.
    /// </summary>
    private SceneBootstrapLoader m_SceneBootstrapLoader;

    private enum ConnectionStates
    {
        None,
        Connecting,
        Connected,
    }

    private ConnectionStates m_ConnectionState;

    [SerializeField]
    private bool m_ServicesRegistered;
#if !DEDICATED_SERVER
    private ISession m_CurrentSession;
    private string m_SessionName;
    private string m_ProfileName;
    private Task m_SessionTask;
#endif
#endregion

    #region Initialization and Destroy
    public static string GetRandomString(int length)
    {
        var r = new System.Random();
        return new string(Enumerable.Range(0, length).Select(_ => (char)r.Next('a', 'z')).ToArray());
    }

    public void SetFrameRate(int targetFrameRate, bool enableVsync)
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = enableVsync ? m_OriginalVSyncCount : 0;
    }

    private void Awake()
    {
#if !DEDICATED_SERVER
        Screen.SetResolution((int)(Screen.currentResolution.width * 0.40f), (int)(Screen.currentResolution.height * 0.40f), FullScreenMode.Windowed);
        SetFrameRate(TargetFrameRate, EnableVSync);
#endif
        SetSingleton();
        m_SceneBootstrapLoader = GetComponent<SceneBootstrapLoader>();
    }

#if DEDICATED_SERVER
    private void Start()
    {
        m_SceneBootstrapLoader.OnMainMenuLoaded += OnMainMenuLoaded;
        m_SceneBootstrapLoader.LoadMainMenu();
    }

    private void OnMainMenuLoaded()
    {
        m_SceneBootstrapLoader.OnMainMenuLoaded -= OnMainMenuLoaded;
        StartDedicatedServer();
    }
#else
    private async void Start()
    {
        OnClientConnectedCallback += OnClientConnected;
        OnClientDisconnectCallback += OnClientDisconnect;
        OnConnectionEvent += OnClientConnectionEvent;

        // Check to see if the project has been registered with an organization before trying to sign in
        if (m_ServicesRegistered)
        {
            if (UnityServices.Instance != null && UnityServices.Instance.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                AuthenticationService.Instance.SignInFailed += SignInFailed;
                AuthenticationService.Instance.SignedIn += SignedIn;
                if (string.IsNullOrEmpty(m_ProfileName))
                {
                    m_ProfileName = GetRandomString(5);
                }
                AuthenticationService.Instance.SwitchProfile(m_ProfileName);
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
        m_SceneBootstrapLoader.LoadMainMenu();

    }
    private void OnDestroy()
    {
        OnClientConnectedCallback -= OnClientConnected;
        OnClientDisconnectCallback -= OnClientDisconnect;
        OnConnectionEvent -= OnClientConnectionEvent;
    }
#endif
#endregion

    #region Session and Connection Event Handling

#if !DEDICATED_SERVER
    private void OnClientConnectionEvent(NetworkManager networkManager, ConnectionEventData eventData)
    {
        LogMessage($"[{Time.realtimeSinceStartup}] Connection event {eventData.EventType} for Client-{eventData.ClientId}.");
    }

    private void OnClientConnected(ulong clientId)
    {
        LogMessage($"[{Time.realtimeSinceStartup}] Connected event invoked for Client-{clientId}.");
    }

    private void OnClientDisconnect(ulong clientId)
    {
        LogMessage($"[{Time.realtimeSinceStartup}] Disconnected event invoked for Client-{clientId}.");
    }

    private void SignedIn()
    {
        AuthenticationService.Instance.SignedIn -= SignedIn;
        Debug.Log($"Signed in anonymously with profile {m_ProfileName}");
    }

    private void SignInFailed(RequestFailedException error)
    {
        AuthenticationService.Instance.SignInFailed -= SignInFailed;
        Debug.LogError($"Failed to sign in {m_ProfileName} anonymously: {error}");
    }

    private void SessionStarted()
    {
        OnClientStarted -= SessionStarted;
        m_ConnectionState = ConnectionStates.Connected;
        if (IsServer)
        {
            LogMessage($"Server started session.");
        }
        else
        {
            LogMessage($"Client connecting to session.");
        }
    }

    private void SessionStopped(bool isHost)
    {
        LogMessage($"NetworkManager has stopped.");
        OnClientStopped -= SessionStopped;
        m_ConnectionState = ConnectionStates.None;
        if (IsServer)
        {
            ResetMainCamera();
        }
    }

    private async Task<ISession> ConnectThroughLiveService()
    {
        try
        {
            var options = new SessionOptions()
            {
                Name = m_SessionName,
                MaxPlayers = 32
            }.WithDistributedAuthorityNetwork();

            m_CurrentSession = await MultiplayerService.Instance.CreateOrJoinSessionAsync(m_SessionName, options);
            return m_CurrentSession;
        }
        catch (Exception e)
        {
            LogMessage($"{e.Message}");
            Debug.LogException(e);
        }
        return null;
    }
#endif
#endregion

    #region GUI Menu
#if !DEDICATED_SERVER
    public void StartOrConnectToDistributedAuthoritySession()
    {
        m_SessionTask = ConnectThroughLiveService();
        m_ConnectionState = ConnectionStates.Connecting;
        LogMessage($"Connecting to session {m_SessionName}...");
    }

    private void OnUpdateGUIDisconnected()
    {
        var width = !m_ServicesRegistered ? 600 : 300;
        GUILayout.BeginArea(new Rect(10, 10, width, 800));

        if (NetworkConfig.NetworkTopology == NetworkTopologyTypes.DistributedAuthority)
        {
            if (!m_ServicesRegistered)
            {
                GUILayout.Label("Project-Settings:Services-General-Settings is not configured.");
                GUILayout.Label("Distributed authority requires project to be registered with your organization's services account for authentication purposes.");
            }
            else
            {
                m_SessionName = GUILayout.TextField(m_SessionName);

                if (GUILayout.Button("Create or Connect To Session"))
                {
                    NetworkConfig.UseCMBService = true;
                    OnClientStopped += SessionStopped;
                    OnClientStarted += SessionStarted;
                    m_SceneBootstrapLoader.StartSession(SceneBootstrapLoader.StartAsTypes.Client);
                }
            }
        }
        else
        {
            if (GUILayout.Button("Start Server"))
            {
                OnServerStopped += SessionStopped;
                OnServerStarted += SessionStarted;
                m_SceneBootstrapLoader.StartSession(SceneBootstrapLoader.StartAsTypes.Server);
            }

            if (GUILayout.Button("Start Host"))
            {
                OnServerStopped += SessionStopped;
                OnServerStarted += SessionStarted;
                m_SceneBootstrapLoader.StartSession(SceneBootstrapLoader.StartAsTypes.Host);
            }

            if (GUILayout.Button("Start Client"))
            {
                OnClientStopped += SessionStopped;
                OnClientStarted += SessionStarted;
                m_SceneBootstrapLoader.StartSession(SceneBootstrapLoader.StartAsTypes.Client);
            }
        }
        GUILayout.EndArea();
    }

    private int OnUpdateGUIConnected(int yAxisOffset)
    {
        GUILayout.BeginArea(new Rect(10, 10, 800, 800));
        if (CMBServiceConnection)
        {
            GUILayout.Label($"Distributed Authority Session: {m_SessionName}");
            if (LocalClient.IsSessionOwner)
            {
                GUILayout.Label("[Session Owner]");
                yAxisOffset += 20;
            }
        }
        else
        {
            GUILayout.Label($"Client-Server Session");
        }
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(Display.main.renderingWidth - 160, 10, 150, 80));
        var endSessionText = IsServer && !DistributedAuthorityMode ? "Shutdown" : "Disconnect";
        if (GUILayout.Button(endSessionText))
        {
            if (m_CurrentSession != null && m_CurrentSession.State == SessionState.Connected)
            {
                m_CurrentSession.LeaveAsync();
                m_CurrentSession = null;
            }
            else
            {
                Shutdown();
            }
        }
        GUILayout.EndArea();

        return yAxisOffset;
    }

    private void OnGUI()
    {
        var yAxisOffset = 10;
        switch (m_ConnectionState)
        {
            case ConnectionStates.None:
                {
                    yAxisOffset = 80;
                    OnUpdateGUIDisconnected();
                    break;
                }
            case ConnectionStates.Connected:
                {
                    yAxisOffset = OnUpdateGUIConnected(40);
                    break;
                }
        }

        GUILayout.BeginArea(new Rect(10, yAxisOffset, 600, 800));
        if (m_MessageLogs.Count > 0)
        {
            GUILayout.Label("-----------(Log)-----------");
            // Display any messages logged to screen
            foreach (var messageLog in m_MessageLogs)
            {
                GUILayout.Label(messageLog.Message);
            }
            GUILayout.Label("---------------------------");
        }
        GUILayout.EndArea();
    }
#endif
#endregion

    #region Server Camera Handling
#if !DEDICATED_SERVER
    private Vector3 m_CameraOriginalPosition;
    private Quaternion m_CameraOriginalRotation;
    private int m_CurrentFollowPlayerIndex = -1;
    private MoverScriptNoRigidbody m_CurrentPlayerFollowed;

    private void ResetMainCamera()
    {
        m_CurrentFollowPlayerIndex = -1;
        SetCameraDefaults();
    }
    private void SetCameraDefaults()
    {
        if (Camera.main != null && Camera.main.transform.parent != null)
        {
            Camera.main.transform.SetParent(null, false);
            Camera.main.transform.position = m_CameraOriginalPosition;
            Camera.main.transform.rotation = m_CameraOriginalRotation;
        }
    }

    /// <summary>
    /// Server only (i.e. not host), follow players as they move around
    /// </summary>
    private void ServerFollowPlayerCheck()
    {
        bool leftBracket = Input.GetKeyDown(KeyCode.LeftBracket);
        bool rightBracket = Input.GetKeyDown(KeyCode.RightBracket);

        if ((leftBracket || rightBracket) && ConnectedClientsIds.Count > 0)
        {
            // Capture the main camera's original position and rotation the first time the server-side
            // follows a player.
            if (m_CurrentFollowPlayerIndex == -1)
            {
                m_CameraOriginalPosition = Camera.main.transform.position;
                m_CameraOriginalRotation = Camera.main.transform.rotation;
            }

            if (leftBracket)
            {
                m_CurrentFollowPlayerIndex--;
                if (m_CurrentFollowPlayerIndex < 0)
                {
                    m_CurrentFollowPlayerIndex = ConnectedClientsIds.Count - 1;
                }
            }
            else
            {
                m_CurrentFollowPlayerIndex++;
            }

            m_CurrentFollowPlayerIndex %= ConnectedClientsIds.Count;

            var playerId = ConnectedClientsIds[m_CurrentFollowPlayerIndex];
            var playerNetworkClient = ConnectedClients[playerId];
            m_CurrentPlayerFollowed = playerNetworkClient.PlayerObject.GetComponent<MoverScriptNoRigidbody>();
            Camera.main.transform.SetParent(playerNetworkClient.PlayerObject.transform, false);
        }
        else if (Input.GetKeyDown(KeyCode.Backspace))
        {
            ClearFollowPlayer();
        }
    }
    public void ClearFollowPlayer()
    {
        if (m_CurrentPlayerFollowed != null)
        {
            m_CurrentPlayerFollowed = null;
            SetCameraDefaults();
        }
    }
#endif
#endregion

    #region Update Methods and Properties
#if DEDICATED_SERVER
    private UnityTransport m_UnityTransport;
    /// <summary>
    /// All of the dedicated server specific script logic is contained and only compiled when DEDICATED_SERVER is defined
    /// </summary>

    private void StartDedicatedServer()
    {
        m_UnityTransport = NetworkConfig.NetworkTransport as UnityTransport;
        if (m_UnityTransport != null)
        {
            // Always good to know what scenes are currently loaded since you might have
            // different scenes to load for a DGS vs client
            var scenesPreloaded = new System.Text.StringBuilder();
            scenesPreloaded.Append("Scenes Preloaded: ");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                scenesPreloaded.Append($"[{scene.name}]");
            }
            Debug.Log(scenesPreloaded.ToString());

            // Set the application frame rate to like 30 to reduce frame processing overhead
            Application.targetFrameRate = 30;

            Debug.Log($"[Pre-Init] Server Address Endpoint: {m_UnityTransport.ConnectionData.ServerEndPoint}");
            Debug.Log($"[Pre-Init] Server Listen Endpoint: {m_UnityTransport.ConnectionData.ListenEndPoint}");
            // Setup your IP and port sepcific to your DGS
            //unityTransport.SetConnectionData(ListenAddress, ListenPort, ListenAddress);

            //Debug.Log($"[Post-Init] Server Address Endpoint: {unityTransport.ConnectionData.ServerEndPoint}");
            //Debug.Log($"[Post-Init] Server Listen Endpoint: {unityTransport.ConnectionData.ListenEndPoint}");

            // Get the server started notification
            OnServerStarted += ServerStarted;

            // Start the server listening
            m_SceneBootstrapLoader.StartSession(SceneBootstrapLoader.StartAsTypes.Server);
        }
        else
        {
            Debug.LogError("Failed to get the UnityTransport!");
        }
    }

    /// <summary>
    /// Register callbacks when the OnServerStarted callback is invoked.
    /// This makes it easier to know you are registering for events only 
    /// when the server successfully has started.
    /// </summary>
    private void ServerStarted()
    {
        Debug.Log("Dedicated Server Started!");
        Debug.Log($"[Started] Server Address Endpoint: {m_UnityTransport.ConnectionData.ServerEndPoint}");
        Debug.Log($"[Started] Server Listen Endpoint: {m_UnityTransport.ConnectionData.ListenEndPoint}");
        Debug.Log("===============================================================");
        Debug.Log("[X] Exits session (Shutdown) | [ESC] Exits application instance");
        Debug.Log("===============================================================");
        OnServerStarted -= ServerStarted;
        OnClientConnectedCallback += ClientConnectedCallback;
        OnClientDisconnectCallback += ClientDisconnectCallback;
        // Register for OnServerStopped
        OnServerStopped += ServerStopped;
    }

    private void ServerStopped(bool obj)
    {
        OnClientConnectedCallback -= ClientConnectedCallback;
        OnClientDisconnectCallback -= ClientDisconnectCallback;
        OnServerStopped -= ServerStopped;
        Debug.Log("Dedicated Server Stopped!");
        Debug.Log("===============================================================");
        Debug.Log("[S] Starts new session (StartServer) | [ESC] Exits application");
        Debug.Log("===============================================================");
    }

    private void ClientConnectedCallback(ulong clientId)
    {
        Debug.Log($"Client-{clientId} connected and approved.");
    }

    private void ClientDisconnectCallback(ulong clientId)
    {
        Debug.Log($"Client-{clientId} disconnected.");
    }

#if UNITY_EDITOR
    private void HandleEditorKeyCommands()
    {
        // Shutdown/Stop the server
        if (Input.GetKeyDown(KeyCode.X) && IsListening && !ShutdownInProgress)
        {
            Shutdown();
        }
        else // Start the server (this example makes it automatically start when the application first starts)
        if (Input.GetKeyDown(KeyCode.S) && !IsListening)
        {
            StartDedicatedServer();
        }
    }
#else
    private void HandleConsoleKeyCommands()
    {
        if (Console.KeyAvailable) 
        {
            var networkManager = NetworkManager.Singleton;
            var keyPressed = Console.ReadKey(true);
            switch(keyPressed.Key) 
            {
                case ConsoleKey.X:
                    {
                        if(networkManager.IsListening && !networkManager.ShutdownInProgress)
                        {
                            networkManager.Shutdown();
                        }
                        break;
                    }
                case ConsoleKey.S:
                    {
                        if (!networkManager.IsListening && !networkManager.ShutdownInProgress)
                        {
                            StartDedicatedServer();
                        }
                        break;
                    }
                case ConsoleKey.Escape:
                    {
                        Application.Quit();
                        break;
                    }
            }
        }
    }
#endif

    /// <summary>
    /// Update that is only included in the build and invoked when running as a dedicated server
    /// </summary>
    private void DedicatedServerUpdate()
    {
#if UNITY_EDITOR
        HandleEditorKeyCommands();
#else
        HandleConsoleKeyCommands();
#endif
    }

#else // End of DEDICATED_SERVER defined region

    /// <summary>
    /// General update for server-side
    /// </summary>
    private void ServerSideUpdate()
    {
        if (!IsHost)
        {
            ServerFollowPlayerCheck();
        }
    }

    /// <summary>
    /// General update for client-side
    /// </summary>
    private void ClientSideUpdate()
    {

    }
#endif

    private void Update()
    {
#if DEDICATED_SERVER
        DedicatedServerUpdate();
#else
        if (IsListening)
        {
            if (IsServer)
            {
                ServerSideUpdate();
            }
            else
            {
                ClientSideUpdate();
            }
        }
#endif

        if (m_MessageLogs.Count == 0)
        {
            return;
        }

        for (int i = m_MessageLogs.Count - 1; i >= 0; i--)
        {
            if (m_MessageLogs[i].ExpirationTime < Time.realtimeSinceStartup)
            {
                m_MessageLogs.RemoveAt(i);
            }
        }
    }

    #endregion

    #region Message Logging

    private List<MessageLog> m_MessageLogs = new List<MessageLog>();

    private class MessageLog
    {
        public string Message { get; private set; }
        public float ExpirationTime { get; private set; }

        public MessageLog(string msg, float timeToLive)
        {
            Message = msg;
            ExpirationTime = Time.realtimeSinceStartup + timeToLive;
        }
    }

    public void LogMessage(string msg, float timeToLive = 10.0f)
    {
        if (m_MessageLogs.Count > 0)
        {
            m_MessageLogs.Insert(0, new MessageLog(msg, timeToLive));
        }
        else
        {
            m_MessageLogs.Add(new MessageLog(msg, timeToLive));
        }

        Debug.Log(msg);
    }
    #endregion

    public NetworkManagerBootstrapper()
    {
        Instance = this;
    }
}
