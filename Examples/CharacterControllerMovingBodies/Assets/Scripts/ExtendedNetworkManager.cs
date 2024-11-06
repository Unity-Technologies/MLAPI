using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using SessionState = Unity.Services.Multiplayer.SessionState;

#if UNITY_EDITOR
using Unity.Netcode.Editor;
using UnityEditor;

/// <summary>
/// The custom editor for the <see cref="ExtendedNetworkManager"/> component.
/// </summary>
[CustomEditor(typeof(ExtendedNetworkManager), true)]
[CanEditMultipleObjects]
public class ExtendedNetworkManagerEditor : NetworkManagerEditor
{
    private SerializedProperty m_ConnectionType;
    private SerializedProperty m_TargetFrameRate;
    private SerializedProperty m_EnableVSync;

    public override void OnEnable()
    {
        m_ConnectionType = serializedObject.FindProperty(nameof(ExtendedNetworkManager.ConnectionType));
        m_TargetFrameRate = serializedObject.FindProperty(nameof(ExtendedNetworkManager.TargetFrameRate));
        m_EnableVSync = serializedObject.FindProperty(nameof(ExtendedNetworkManager.EnableVSync));
        base.OnEnable();
    }

    private void DisplayExtendedNetworkManagerProperties()
    {
        EditorGUILayout.PropertyField(m_ConnectionType);
        EditorGUILayout.PropertyField(m_TargetFrameRate);
        EditorGUILayout.PropertyField(m_EnableVSync);
    }

    public override void OnInspectorGUI()
    {
        var extendedNetworkManager = target as ExtendedNetworkManager;
        // Handle switching the appropriate connection type based on the network topology
        // Host connectio type can be set for client-server and distributed authority
        // Live Service can only be used with distributed authority
        // Client-server can only be used with a host connection type
        var connectionTypes = Enum.GetValues(typeof(ExtendedNetworkManager.ConnectionTypes));
        var connectionType = ExtendedNetworkManager.ConnectionTypes.LiveService;
        if (m_ConnectionType.enumValueIndex > 0 && m_ConnectionType.enumValueIndex < connectionTypes.Length)
        {
            connectionType = (ExtendedNetworkManager.ConnectionTypes)connectionTypes.GetValue(m_ConnectionType.enumValueIndex);
        }
        void SetExpanded(bool expanded) { extendedNetworkManager.ExtendedNetworkManagerExpanded = expanded; };
        DrawFoldOutGroup<ExtendedNetworkManager>(extendedNetworkManager.GetType(), DisplayExtendedNetworkManagerProperties, extendedNetworkManager.ExtendedNetworkManagerExpanded, SetExpanded);

        var updatedConnectedType = (ExtendedNetworkManager.ConnectionTypes)connectionTypes.GetValue(m_ConnectionType.enumValueIndex);
        if (connectionType == updatedConnectedType && updatedConnectedType == ExtendedNetworkManager.ConnectionTypes.LiveService && extendedNetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.ClientServer)
        {
            extendedNetworkManager.ConnectionType = ExtendedNetworkManager.ConnectionTypes.Host;
        }
        else if (connectionType == ExtendedNetworkManager.ConnectionTypes.Host && updatedConnectedType == ExtendedNetworkManager.ConnectionTypes.LiveService && extendedNetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.ClientServer)
        {
            extendedNetworkManager.NetworkConfig.NetworkTopology = NetworkTopologyTypes.DistributedAuthority;
        }
        base.OnInspectorGUI();
    }
}
#endif



public class ExtendedNetworkManager : NetworkManager
{
#if UNITY_EDITOR
    // Inspector view expand/collapse settings for this derived child class
    [HideInInspector]
    public bool ExtendedNetworkManagerExpanded;
#endif

    public static ExtendedNetworkManager Instance;

    public enum ConnectionTypes
    {
        LiveService,
        Host,
    }
    public ConnectionTypes ConnectionType;

    public int TargetFrameRate = 100;
    public bool EnableVSync = false;

    [HideInInspector]
    [SerializeField]
    private int m_OriginalVSyncCount;

#if UNITY_EDITOR

    protected override void OnValidateComponent()
    {
        m_OriginalVSyncCount = QualitySettings.vSyncCount;
        base.OnValidateComponent();
    }
#endif

    private ISession m_CurrentSession;

    private string m_SessionName;
    private string m_ProfileName;
    private Task m_SessionTask;

    private enum ConnectionStates
    {
        None,
        Connecting,
        Connected,
    }

    private ConnectionStates m_ConnectionState;

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
        Screen.SetResolution((int)(Screen.currentResolution.width * 0.40f), (int)(Screen.currentResolution.height * 0.40f), FullScreenMode.Windowed);
        SetFrameRate(TargetFrameRate, EnableVSync);
        SetSingleton();
    }

    private async void Start()
    {
        OnClientConnectedCallback += OnClientConnected;
        OnClientDisconnectCallback += OnClientDisconnect;
        OnConnectionEvent += OnClientConnectionEvent;
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

    private void OnDestroy()
    {
        OnClientConnectedCallback -= OnClientConnected;
        OnClientDisconnectCallback -= OnClientDisconnect;
        OnConnectionEvent -= OnClientConnectionEvent;
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

    private void OnDrawLiveServiceGUI()
    {
        m_SessionName = GUILayout.TextField(m_SessionName);

        if (GUILayout.Button("Create or Connect To Session"))
        {
            NetworkConfig.UseCMBService = true;
            OnClientStopped += ClientStopped;
            OnClientStarted += ClientStarted;
            m_SessionTask = ConnectThroughLiveService();
            m_ConnectionState = ConnectionStates.Connecting;
            LogMessage($"Connecting to session {m_SessionName}...");
        }
    }

    private void OnDrawDAHostGUI()
    {
        if (GUILayout.Button("Start Host"))
        {
            OnClientStopped += ClientStopped;
            OnClientStarted += ClientStarted;
            StartHost();
        }

        if (GUILayout.Button("Start Client"))
        {
            OnClientStopped += ClientStopped;
            OnClientStarted += ClientStarted;
            StartClient();
        }
    }

    private void OnUpdateGUIDisconnected()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 800));

        GUILayout.Label("Session Name", GUILayout.Width(100));

        var connectionType = ConnectionType;
        if (NetworkConfig.NetworkTopology == NetworkTopologyTypes.ClientServer && connectionType != ConnectionTypes.Host)
        {
            connectionType = ConnectionTypes.Host;
        }

        switch (connectionType)
        {
            case ConnectionTypes.LiveService:
                {
                    OnDrawLiveServiceGUI();
                    break;
                }
            case ConnectionTypes.Host:
                {
                    OnDrawDAHostGUI();
                    break;
                }
        }

        GUILayout.EndArea();

        GUILayout.BeginArea(new Rect(10, Display.main.renderingHeight - 40, Display.main.renderingWidth - 10, 30));
        var scenesPreloaded = new System.Text.StringBuilder();
        scenesPreloaded.Append("Scenes Preloaded: ");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            scenesPreloaded.Append($"[{scene.name}]");
        }
        GUILayout.Label(scenesPreloaded.ToString());
        GUILayout.EndArea();
    }

    private void OnUpdateGUIConnected()
    {
        if (CMBServiceConnection)
        {
            GUILayout.BeginArea(new Rect(10, 10, 800, 800));
            GUILayout.Label($"Session: {m_SessionName}");
            GUILayout.EndArea();
        }
        else
        {
            GUILayout.BeginArea(new Rect(10, 10, 800, 800));
            if (DistributedAuthorityMode)
            {
                GUILayout.Label($"DAHosted Session");
            }
            else
            {
                GUILayout.Label($"Client-Server Session");
            }

            GUILayout.EndArea();
        }

        GUILayout.BeginArea(new Rect(Display.main.renderingWidth - 160, 10, 150, 80));

        if (GUILayout.Button("Disconnect"))
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

    private void ClientStarted()
    {
        OnClientStarted -= ClientStarted;
        m_ConnectionState = ConnectionStates.Connected;
        LogMessage($"Connected to session {m_SessionName}.");
    }

    private void ClientStopped(bool isHost)
    {
        OnClientStopped -= ClientStopped;
        m_ConnectionState = ConnectionStates.None;
        m_SessionTask = null;
        m_CurrentSession = null;
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

    private void Update()
    {
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

    public ExtendedNetworkManager()
    {
        Instance = this;
    }
}
