using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;


namespace TestProject.ManualTests
{
    public class StatsDisplay : NetworkBehaviour
    {
        [SerializeField]
        private GameObject m_ClientServerToggle;

        [Tooltip("When enabled, this will display all scene events in the display window.")]
        [SerializeField]
        private bool m_TrackSceneEvents;

        [Tooltip("When enabled, this will log all messages to the console.")]
        [SerializeField]
        private bool m_LogSceneEventsToConsole;

        private bool m_ClientMode = true;
        private Rect m_Stats;
        private string m_LastStatsDump;
        private Text m_ClientServerToggleText;
        private List<ulong> m_ClientsToUpdate = new List<ulong>();

        private bool m_IsServer;

        private SceneEventNotificationQueue m_SceneEventNotificationQueue;

        private void Start()
        {
            GUI.contentColor = new Color(196, 196, 196, 196);
            GUI.backgroundColor = new Color(96, 96, 96, 96);
            if (m_ClientServerToggle != null)
            {
                m_ClientServerToggleText = m_ClientServerToggle.GetComponentInChildren<Text>();
            }

            if (NetworkManager && NetworkManager.IsListening && !NetworkManager.IsServer)
            {
                m_ClientServerToggle.SetActive(true);
            }
            else
            {
                m_ClientServerToggle.SetActive(false);
            }
        }

        public override void OnNetworkSpawn()
        {
            if (NetworkManager.IsServer)
            {
                m_IsServer = true;
                NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
            }
            else
            {
                m_ClientServerToggle.SetActive(true);
                UpdateButton();
            }

            if (m_TrackSceneEvents)
            {
                m_SceneEventNotificationQueue = NetworkManager.gameObject.GetComponent<SceneEventNotificationQueue>();
            }

            StartCoroutine("UpdateTextStatus");
        }

        /// <summary>
        /// Invoked when a client connects
        /// </summary>
        /// <param name="clientId"></param>
        private void OnClientConnectedCallback(ulong clientId)
        {
            if (!IsServer)
            {
                var networkObject = GetComponent<NetworkObject>();
                if (networkObject != null)
                {
                    networkObject.SpawnWithOwnership(clientId, true);
                }
            }
        }

        /// <summary>
        /// Remove our OnClientConnectedCallback registration when we are destroyed
        /// </summary>
        private void OnDestroy()
        {
            if (NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
            }
        }

        /// <summary>
        /// Used by UI Button click event
        /// </summary>
        public void ToggleClientSever()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                m_ClientMode = !m_ClientMode;
                GetStatsServerRPC(NetworkManager.Singleton.LocalClientId);
                UpdateButton();
            }
        }

        /// <summary>
        /// Draw the stats
        /// </summary>
        private void OnGUI()
        {
            if (NetworkManager && NetworkManager.IsListening)
            {
                var width = 0.25f * Screen.width;
                var height = 0.50f * Screen.height;
                m_Stats = new Rect(5, 10, width, height);
                GUI.TextArea(m_Stats, m_LastStatsDump);
            }
        }

        /// <summary>
        /// Updates the text of the button for switching between server and client stats
        /// </summary>
        private void UpdateButton()
        {
            if (m_ClientServerToggleText != null)
            {
                m_ClientServerToggleText.text = m_ClientMode ? "Show Server Stats" : "Show Client Stats";
            }
        }

        /// <summary>
        /// RPC Used for the server to send stats info to the client
        /// </summary>
        /// <param name="statsinfo"></param>
        [ClientRpc]
        private void ReceiveStatsClientRpc(StatsInfoContainer statsinfo)
        {
            m_LastStatsDump = "Server Stats";
            m_LastStatsDump += "\ndeltaTime: [" + statsinfo.StatValues[0] + "]";
            m_LastStatsDump += $"\nActive Scene: {SceneManager.GetActiveScene().name}";
        }

        /// <summary>
        /// RPC used to notify server that a specific client wants to receive its stats info
        /// </summary>
        /// <param name="clientId"></param>
        [ServerRpc(RequireOwnership = false)]
        public void GetStatsServerRPC(ulong clientId)
        {
            if (!m_ClientsToUpdate.Contains(clientId))
            {
                m_ClientsToUpdate.Add(clientId);
            }
            else if (m_ClientsToUpdate.Contains(clientId))
            {
                m_ClientsToUpdate.Remove(clientId);
            }
        }

        /// <summary>
        /// Coroutine to update the stats information
        /// </summary>
        /// <returns></returns>
        private IEnumerator UpdateTextStatus()
        {
            while (true)
            {
                if (IsSpawned)
                {
                    if (m_ClientMode)
                    {
                        m_LastStatsDump = m_IsServer ? "Server Stats" : "Client Stats";
                        m_LastStatsDump += "\ndeltaTime: [" + Time.deltaTime.ToString() + "]\n";
                        m_LastStatsDump += "Spawned: [" + NetworkManager.SpawnManager.SpawnedObjectsList.Count + "]\n";
                        m_LastStatsDump += $"Active Scene: {SceneManager.GetActiveScene().name}\n";
                        if (m_SceneEventNotificationQueue != null)
                        {
                            var sceneEvents = m_SceneEventNotificationQueue.GetCurrentNotifications();
                            m_LastStatsDump += $"Scene Events {sceneEvents.Count}:\n";
                            foreach (var sceneEventEntry in sceneEvents)
                            {
                                m_LastStatsDump += sceneEventEntry + "\n";
                            }
                        }
                    }
                    if (NetworkManager.Singleton.IsServer && m_ClientsToUpdate.Count > 0)
                    {
                        var statsInfoContainer = new StatsInfoContainer();
                        statsInfoContainer.StatValues = new List<float>();
                        statsInfoContainer.StatValues.Add(Time.deltaTime);
                        ReceiveStatsClientRpc(statsInfoContainer);
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
