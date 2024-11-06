using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Examples.PingTool;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;


public class NetworkManagerHelper : MonoBehaviour
{
    public static NetworkManagerHelper Instance;

    public enum ConnectionTypes
    {
        Service,
        Host,
    }

    [Tooltip("The connection type. When set to service, it will connect to the local service. When host, it will use the standard host connection.")]
    public ConnectionTypes ConnectionType;

    [Tooltip("Enables or disables VSynch.")]
    public bool EnableVSync = false;

    [Tooltip("Sets the desired target frame rate.")]
    public int TargetFrameRate = 100;

    [HideInInspector]
    [SerializeField]
    private int m_OriginalVSyncCount;

    private NetworkManager m_NetworkManager;
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        m_NetworkManager = GetComponent<NetworkManager>();
        m_OriginalVSyncCount = QualitySettings.vSyncCount;
    }
#endif

    public void LogPingToolMessage(ulong clientId, PingTool.ClientRtt clientRtt)
    {
        LogMessage($"[Client-{clientId}] Ping: {clientRtt.Ping * 1000}ms | RTT: {clientRtt.RTT}ms", 5);
    }

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
        m_NetworkManager = GetComponent<NetworkManager>();
    }

    private async void Start()
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
            m_NetworkManager.NetworkConfig.UseCMBService = true;
            m_NetworkManager.OnClientStopped += OnClientStopped;
            m_NetworkManager.OnClientStarted += OnClientStarted;
            m_SessionTask = ConnectThroughLiveService();
            m_ConnectionState = ConnectionStates.Connecting;
            LogMessage($"Connecting to session {m_SessionName}...");
        }
    }

    private void OnDrawLocalServiceGUI()
    {
        var unityTransport = m_NetworkManager.NetworkConfig.NetworkTransport as UnityTransport;
        GUILayout.Label("IP Address:", GUILayout.Width(100));
        unityTransport.ConnectionData.Address = GUILayout.TextField(unityTransport.ConnectionData.Address, GUILayout.Width(100));

        GUILayout.Label("Port:", GUILayout.Width(100));
        var portString = GUILayout.TextField(unityTransport.ConnectionData.Port.ToString(), GUILayout.Width(100));
        ushort.TryParse(portString, out unityTransport.ConnectionData.Port);

        // CMB distributed authority services just "connects" with no host, client, or server option (all are clients)
        if (GUILayout.Button("Start Client"))
        {
            m_NetworkManager.NetworkConfig.UseCMBService = true;
            m_NetworkManager.OnClientStopped += OnClientStopped;
            m_NetworkManager.OnClientStarted += OnClientStarted;
            m_NetworkManager.StartClient();
            m_ConnectionState = ConnectionStates.Connecting;
        }
    }

    private void OnDrawDAHostGUI()
    {
        if (GUILayout.Button("Start Host"))
        {
            m_NetworkManager.OnClientStopped += OnClientStopped;
            m_NetworkManager.OnClientStarted += OnClientStarted;
            m_NetworkManager.StartHost();
        }

        if (GUILayout.Button("Start Client"))
        {
            m_NetworkManager.OnClientStopped += OnClientStopped;
            m_NetworkManager.OnClientStarted += OnClientStarted;
            m_NetworkManager.StartClient();
        }
    }


    private void OnUpdateGUIDisconnected()
    {
        GUILayout.BeginArea(new Rect(10, 10, 300, 800));

        GUILayout.Label("Session Name", GUILayout.Width(100));

        var connectionType = ConnectionType;
        if (m_NetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.ClientServer && connectionType != ConnectionTypes.Service)
        {
            connectionType = ConnectionTypes.Host;
        }

        switch (connectionType)
        {
            case ConnectionTypes.Service:
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
    }

    private void OnUpdateGUIConnected()
    {
        GUILayout.BeginArea(new Rect(10, 10, 800, 40));
        if (m_NetworkManager.CMBServiceConnection)
        {
            GUILayout.Label($"Session: {m_SessionName}");
        }
        else
        {
            var unityTransport = m_NetworkManager.NetworkConfig.NetworkTransport as UnityTransport;
            GUILayout.Label($"IP: {unityTransport.ConnectionData.Address} : {unityTransport.ConnectionData.Port}");
        }

        GUILayout.EndArea();

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
                m_NetworkManager.Shutdown();
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

    private void OnClientStarted()
    {
        m_NetworkManager.OnClientStarted -= OnClientStarted;
        m_ConnectionState = ConnectionStates.Connected;
        LogMessage($"Connected to session {m_SessionName}.");
    }

    private void OnClientStopped(bool isHost)
    {
        m_NetworkManager.OnClientStopped -= OnClientStopped;
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
            m_ConnectionState = ConnectionStates.None;
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

    public NetworkManagerHelper()
    {
        Instance = this;
    }
}
