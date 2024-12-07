using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

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
#if UNITY_EDITOR
    // Inspector view expand/collapse settings for this derived child class
    [HideInInspector]
    public bool NetworkManagerBootstrapperExpanded;
    protected override void OnValidateComponent()
    {
        m_OriginalVSyncCount = QualitySettings.vSyncCount;
        base.OnValidateComponent();
    }
#endif

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

    public void SetFrameRate(int targetFrameRate, bool enableVsync)
    {
        Application.targetFrameRate = targetFrameRate;
        QualitySettings.vSyncCount = enableVsync ? m_OriginalVSyncCount : 0;
    }

    private void Awake()
    {
        Screen.SetResolution((int)(Screen.currentResolution.width * 0.40f), (int)(Screen.currentResolution.height * 0.40f), FullScreenMode.Windowed);
        SetFrameRate(TargetFrameRate, EnableVSync);
        SetSingleton();
        m_SceneBootstrapLoader = GetComponent<SceneBootstrapLoader>();
    }

    private void Start()
    {
        OnClientConnectedCallback += OnClientConnected;
        OnClientDisconnectCallback += OnClientDisconnect;
        OnConnectionEvent += OnClientConnectionEvent;
        m_SceneBootstrapLoader.LoadMainMenu();
    }

    private void OnDestroy()
    {
        OnClientConnectedCallback -= OnClientConnected;
        OnClientDisconnectCallback -= OnClientDisconnect;
        OnConnectionEvent -= OnClientConnectionEvent;
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

    private void OnUpdateGUIDisconnected()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 800));
        if (GUILayout.Button("Start Server"))
        {
            OnServerStopped += SessionStopped;
            OnServerStarted += SessionStarted;
            m_SceneBootstrapLoader.StartSession(true);
        }

        if (GUILayout.Button("Start Client"))
        {
            OnClientStopped += SessionStopped;
            OnClientStarted += SessionStarted;
            m_SceneBootstrapLoader.StartSession(false);
        }
        GUILayout.EndArea();
    }

    private void OnUpdateGUIConnected()
    {
        GUILayout.BeginArea(new Rect(10, 10, 800, 800));
        GUILayout.Label($"Client-Server Session");
        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(Display.main.renderingWidth - 160, 10, 150, 80));
        var endSessionText = IsServer ? "Shutdown" : "Disconnect";
        if (GUILayout.Button(endSessionText))
        {
            Shutdown();
        }
        GUILayout.EndArea();
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
                    yAxisOffset = 40;
                    OnUpdateGUIConnected();
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

    /// <summary>
    /// General update for client-side
    /// </summary>
    private void ClientSideUpdate()
    {

    }

    private Vector3 m_CameraOriginalPosition;
    private Quaternion m_CameraOriginalRotation;
    private int m_CurrentFollowPlayerIndex = -1;

    private void ResetMainCamera()
    {
        m_CurrentFollowPlayerIndex = -1;
        if (Camera.main != null && Camera.main.transform.parent != null)
        {
            Camera.main.transform.SetParent(null, false);
            Camera.main.transform.position = m_CameraOriginalPosition;
            Camera.main.transform.rotation = m_CameraOriginalRotation;
        }
    }

    /// <summary>
    /// General update for server-side
    /// </summary>
    private void ServerSideUpdate()
    {
        if (Input.GetKeyDown(KeyCode.P) && ConnectedClientsIds.Count > 0)
        {
            // Capture the main camera's original position and rotation the first time the server-side
            // follows a player.
            if (m_CurrentFollowPlayerIndex == -1)
            {
                m_CameraOriginalPosition = Camera.main.transform.position;
                m_CameraOriginalRotation = Camera.main.transform.rotation;
            }
            m_CurrentFollowPlayerIndex++;
            m_CurrentFollowPlayerIndex %= ConnectedClientsIds.Count;

            var playerId = ConnectedClientsIds[m_CurrentFollowPlayerIndex];
            var playerObject = ConnectedClients[playerId];
            Camera.main.transform.SetParent(playerObject.PlayerObject.transform, false);
        }
        else if (Input.GetKeyDown(KeyCode.Backspace))
        {
            Camera.main.transform.SetParent(null, false);
            Camera.main.transform.position = m_CameraOriginalPosition;
            Camera.main.transform.rotation = m_CameraOriginalRotation;
        }
    }

    private void Update()
    {
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

    public NetworkManagerBootstrapper()
    {
        Instance = this;
    }
}
