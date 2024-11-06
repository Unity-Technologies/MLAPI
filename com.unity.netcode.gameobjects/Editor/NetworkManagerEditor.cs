using System;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace Unity.Netcode.Editor
{
    /// <summary>
    /// This <see cref="CustomEditor"/> handles the translation between the <see cref="NetworkConfig"/> and
    /// the <see cref="NetworkManager"/> properties.
    /// </summary>
    [CustomEditor(typeof(NetworkManager), true)]
    [CanEditMultipleObjects]
    public class NetworkManagerEditor : NetcodeEditorBase<NetworkManager>
    {
        private static GUIStyle s_CenteredWordWrappedLabelStyle;
        private static GUIStyle s_HelpBoxStyle;

        // Properties
        private SerializedProperty m_RunInBackgroundProperty;
        private SerializedProperty m_LogLevelProperty;

        // NetworkConfig
        private SerializedProperty m_NetworkConfigProperty;

        // NetworkConfig fields
        private SerializedProperty m_PlayerPrefabProperty;
        private SerializedProperty m_ProtocolVersionProperty;
        private SerializedProperty m_NetworkTransportProperty;
        private SerializedProperty m_TickRateProperty;
#if MULTIPLAYER_SERVICES_SDK_INSTALLED
        private SerializedProperty m_AutoSpawnPlayerPrefabClientSide;
        private SerializedProperty m_NetworkTopologyProperty;
#endif
        private SerializedProperty m_ClientConnectionBufferTimeoutProperty;
        private SerializedProperty m_ConnectionApprovalProperty;
        private SerializedProperty m_EnsureNetworkVariableLengthSafetyProperty;
        private SerializedProperty m_ForceSamePrefabsProperty;
        private SerializedProperty m_EnableSceneManagementProperty;
        private SerializedProperty m_RecycleNetworkIdsProperty;
        private SerializedProperty m_NetworkIdRecycleDelayProperty;
        private SerializedProperty m_SpawnTimeOutProperty;
        private SerializedProperty m_RpcHashSizeProperty;
        private SerializedProperty m_LoadSceneTimeOutProperty;
        private SerializedProperty m_PrefabsList;

        private SerializedProperty m_NetworkProfileMetrics;
        private SerializedProperty m_NetworkMessageMetrics;

        private NetworkManager m_NetworkManager;
        private bool m_Initialized;

        private readonly List<Type> m_TransportTypes = new List<Type>();
        private string[] m_TransportNames = { "Select transport..." };

        private void ReloadTransports()
        {
            m_TransportTypes.Clear();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes();

                foreach (var type in types)
                {
                    if (type.IsSubclassOf(typeof(NetworkTransport)) && !type.IsSubclassOf(typeof(TestingNetworkTransport)) && type != typeof(TestingNetworkTransport))
                    {
                        m_TransportTypes.Add(type);
                    }
                }
            }

            m_TransportNames = new string[m_TransportTypes.Count + 1];
            m_TransportNames[0] = "Select transport...";

            for (int i = 0; i < m_TransportTypes.Count; i++)
            {
                m_TransportNames[i + 1] = m_TransportTypes[i].Name;
            }
        }

        private void Initialize()
        {
            if (m_Initialized)
            {
                return;
            }

            m_Initialized = true;
            m_NetworkManager = (NetworkManager)target;

            // Base properties
            m_RunInBackgroundProperty = serializedObject.FindProperty(nameof(NetworkManager.RunInBackground));
            m_LogLevelProperty = serializedObject.FindProperty(nameof(NetworkManager.LogLevel));
            m_NetworkConfigProperty = serializedObject.FindProperty(nameof(NetworkManager.NetworkConfig));

            // NetworkConfig properties
            m_PlayerPrefabProperty = m_NetworkConfigProperty.FindPropertyRelative(nameof(NetworkConfig.PlayerPrefab));
            m_ProtocolVersionProperty = m_NetworkConfigProperty.FindPropertyRelative("ProtocolVersion");
            m_NetworkTransportProperty = m_NetworkConfigProperty.FindPropertyRelative("NetworkTransport");
            m_TickRateProperty = m_NetworkConfigProperty.FindPropertyRelative("TickRate");
#if MULTIPLAYER_SERVICES_SDK_INSTALLED
            m_NetworkTopologyProperty = m_NetworkConfigProperty.FindPropertyRelative("NetworkTopology");
            // Only display the auto spawn property when the distributed authority network topology is selected
            if (m_NetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.DistributedAuthority)
            {
                m_AutoSpawnPlayerPrefabClientSide = m_NetworkConfigProperty.FindPropertyRelative("AutoSpawnPlayerPrefabClientSide");
            }
#endif
            m_ClientConnectionBufferTimeoutProperty = m_NetworkConfigProperty.FindPropertyRelative("ClientConnectionBufferTimeout");
            m_ConnectionApprovalProperty = m_NetworkConfigProperty.FindPropertyRelative("ConnectionApproval");
            m_EnsureNetworkVariableLengthSafetyProperty = m_NetworkConfigProperty.FindPropertyRelative("EnsureNetworkVariableLengthSafety");
            m_ForceSamePrefabsProperty = m_NetworkConfigProperty.FindPropertyRelative("ForceSamePrefabs");
            m_RecycleNetworkIdsProperty = m_NetworkConfigProperty.FindPropertyRelative("RecycleNetworkIds");
            m_NetworkIdRecycleDelayProperty = m_NetworkConfigProperty.FindPropertyRelative("NetworkIdRecycleDelay");

            m_EnableSceneManagementProperty = m_NetworkConfigProperty.FindPropertyRelative("EnableSceneManagement");
            m_SpawnTimeOutProperty = m_NetworkConfigProperty.FindPropertyRelative("SpawnTimeout");
            m_LoadSceneTimeOutProperty = m_NetworkConfigProperty.FindPropertyRelative("LoadSceneTimeOut");

            m_NetworkProfileMetrics = m_NetworkConfigProperty.FindPropertyRelative("NetworkProfileMetrics");
#if MULTIPLAYER_TOOLS
            m_NetworkMessageMetrics = m_NetworkConfigProperty.FindPropertyRelative("NetworkMessageMetrics");
#endif
            m_RpcHashSizeProperty = m_NetworkConfigProperty.FindPropertyRelative("RpcHashSize");
            m_PrefabsList = m_NetworkConfigProperty
                .FindPropertyRelative(nameof(NetworkConfig.Prefabs))
                .FindPropertyRelative(nameof(NetworkPrefabs.NetworkPrefabsLists));

            ReloadTransports();
        }

        private void CheckNullProperties()
        {
            // Base properties
            m_RunInBackgroundProperty = serializedObject.FindProperty(nameof(NetworkManager.RunInBackground));
            m_LogLevelProperty = serializedObject.FindProperty(nameof(NetworkManager.LogLevel));
            m_NetworkConfigProperty = serializedObject.FindProperty(nameof(NetworkManager.NetworkConfig));

            // NetworkConfig properties
            m_PlayerPrefabProperty = m_NetworkConfigProperty.FindPropertyRelative(nameof(NetworkConfig.PlayerPrefab));
            m_ProtocolVersionProperty = m_NetworkConfigProperty.FindPropertyRelative("ProtocolVersion");
            m_NetworkTransportProperty = m_NetworkConfigProperty.FindPropertyRelative("NetworkTransport");
            m_TickRateProperty = m_NetworkConfigProperty.FindPropertyRelative("TickRate");
#if MULTIPLAYER_SERVICES_SDK_INSTALLED
            m_NetworkTopologyProperty = m_NetworkConfigProperty.FindPropertyRelative("NetworkTopology");
            // Only display the auto spawn property when the distributed authority network topology is selected
            if (m_NetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.DistributedAuthority)
            {
                m_AutoSpawnPlayerPrefabClientSide = m_NetworkConfigProperty.FindPropertyRelative("AutoSpawnPlayerPrefabClientSide");
            }
#endif
            m_ClientConnectionBufferTimeoutProperty = m_NetworkConfigProperty.FindPropertyRelative("ClientConnectionBufferTimeout");
            m_ConnectionApprovalProperty = m_NetworkConfigProperty.FindPropertyRelative("ConnectionApproval");
            m_EnsureNetworkVariableLengthSafetyProperty = m_NetworkConfigProperty.FindPropertyRelative("EnsureNetworkVariableLengthSafety");
            m_ForceSamePrefabsProperty = m_NetworkConfigProperty.FindPropertyRelative("ForceSamePrefabs");
            m_RecycleNetworkIdsProperty = m_NetworkConfigProperty.FindPropertyRelative("RecycleNetworkIds");
            m_NetworkIdRecycleDelayProperty = m_NetworkConfigProperty.FindPropertyRelative("NetworkIdRecycleDelay");


            m_EnableSceneManagementProperty = m_NetworkConfigProperty.FindPropertyRelative("EnableSceneManagement");
            m_SpawnTimeOutProperty = m_NetworkConfigProperty.FindPropertyRelative("SpawnTimeout");
            m_LoadSceneTimeOutProperty = m_NetworkConfigProperty.FindPropertyRelative("LoadSceneTimeOut");

            m_NetworkProfileMetrics = m_NetworkConfigProperty.FindPropertyRelative("NetworkProfilingMetrics");
#if MULTIPLAYER_TOOLS
            m_NetworkMessageMetrics = m_NetworkConfigProperty.FindPropertyRelative("NetworkMessageMetrics");
#endif

            m_RpcHashSizeProperty = m_NetworkConfigProperty.FindPropertyRelative("RpcHashSize");
            m_PrefabsList = m_NetworkConfigProperty
                .FindPropertyRelative(nameof(NetworkConfig.Prefabs))
                .FindPropertyRelative(nameof(NetworkPrefabs.NetworkPrefabsLists));
        }

        private void DisplayNetworkManagerProperties()
        {
            if (!m_NetworkManager.IsServer && !m_NetworkManager.IsClient)
            {
                serializedObject.Update();

                EditorGUILayout.PropertyField(m_RunInBackgroundProperty);
                EditorGUILayout.PropertyField(m_LogLevelProperty);
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Network Settings", EditorStyles.boldLabel);
#if MULTIPLAYER_SERVICES_SDK_INSTALLED
                EditorGUILayout.PropertyField(m_NetworkTopologyProperty);
#endif
                EditorGUILayout.PropertyField(m_ProtocolVersionProperty);
                EditorGUILayout.PropertyField(m_NetworkTransportProperty);
                if (m_NetworkTransportProperty.objectReferenceValue == null)
                {
                    EditorGUILayout.HelpBox("You have no transport selected. A transport is required for netcode to work. Which one do you want?", MessageType.Warning);

                    int selection = EditorGUILayout.Popup(0, m_TransportNames);

                    if (selection > 0)
                    {
                        ReloadTransports();

                        var transportComponent = m_NetworkManager.gameObject.GetComponent(m_TransportTypes[selection - 1]) ?? m_NetworkManager.gameObject.AddComponent(m_TransportTypes[selection - 1]);
                        m_NetworkTransportProperty.objectReferenceValue = transportComponent;

                        Repaint();
                    }
                }
                EditorGUILayout.PropertyField(m_TickRateProperty);
                EditorGUILayout.PropertyField(m_SpawnTimeOutProperty);
                EditorGUILayout.PropertyField(m_ConnectionApprovalProperty);
                if (m_NetworkManager.NetworkConfig.ConnectionApproval)
                {
                    EditorGUILayout.PropertyField(m_ClientConnectionBufferTimeoutProperty);
                }
                EditorGUILayout.PropertyField(m_EnsureNetworkVariableLengthSafetyProperty, new GUIContent("NetworkVariable Length Safety"));
                EditorGUILayout.PropertyField(m_RecycleNetworkIdsProperty);
                if (m_NetworkManager.NetworkConfig.RecycleNetworkIds)
                {
                    EditorGUILayout.PropertyField(m_NetworkIdRecycleDelayProperty);
                }
                EditorGUILayout.PropertyField(m_RpcHashSizeProperty);

                EditorGUILayout.PropertyField(m_NetworkProfileMetrics);
#if MULTIPLAYER_TOOLS
                EditorGUILayout.PropertyField(m_NetworkMessageMetrics);
#endif

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Prefab Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(m_ForceSamePrefabsProperty);
#if MULTIPLAYER_SERVICES_SDK_INSTALLED
                // Only display the auto spawn property when the distributed authority network topology is selected
                if (m_NetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.DistributedAuthority)
                {
                    EditorGUILayout.PropertyField(m_AutoSpawnPlayerPrefabClientSide, new GUIContent("Auto Spawn Player Prefab"));
                }                
#endif
                EditorGUILayout.PropertyField(m_PlayerPrefabProperty, new GUIContent("Default Player Prefab"));



                if (m_NetworkManager.NetworkConfig.HasOldPrefabList())
                {
                    EditorGUILayout.HelpBox("Network Prefabs serialized in old format. Migrate to new format to edit the list.", MessageType.Info);
                    if (GUILayout.Button(new GUIContent("Migrate Prefab List", "Converts the old format Network Prefab list to a new Scriptable Object")))
                    {
                        // Default directory
                        var directory = "Assets/";
                        var assetPath = AssetDatabase.GetAssetPath(m_NetworkManager);
                        if (assetPath == "")
                        {
                            assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(m_NetworkManager);
                        }

                        if (assetPath != "")
                        {
                            directory = Path.GetDirectoryName(assetPath);
                        }
                        else
                        {
#if UNITY_2021_1_OR_NEWER
                            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(m_NetworkManager.gameObject);
#else
                            var prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetPrefabStage(m_NetworkManager.gameObject);
#endif
                            if (prefabStage != null)
                            {
                                var prefabPath = prefabStage.assetPath;
                                if (!string.IsNullOrEmpty(prefabPath))
                                {
                                    directory = Path.GetDirectoryName(prefabPath);
                                }
                            }
                            if (m_NetworkManager.gameObject.scene != null)
                            {
                                var scenePath = m_NetworkManager.gameObject.scene.path;
                                if (!string.IsNullOrEmpty(scenePath))
                                {
                                    directory = Path.GetDirectoryName(scenePath);
                                }
                            }
                        }
                        var networkPrefabs = m_NetworkManager.NetworkConfig.MigrateOldNetworkPrefabsToNetworkPrefabsList();
                        string path = Path.Combine(directory, $"NetworkPrefabs-{m_NetworkManager.GetInstanceID()}.asset");
                        Debug.Log("Saving migrated Network Prefabs List to " + path);
                        AssetDatabase.CreateAsset(networkPrefabs, path);
                        EditorUtility.SetDirty(m_NetworkManager);
                    }
                }
                else
                {
                    if (m_NetworkManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Count == 0)
                    {
                        EditorGUILayout.HelpBox("You have no prefab list selected. You will have to add your prefabs manually at runtime for netcode to work.", MessageType.Warning);
                    }
                    EditorGUILayout.PropertyField(m_PrefabsList);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Scene Management Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(m_EnableSceneManagementProperty);
                if (m_NetworkManager.NetworkConfig.EnableSceneManagement)
                {
                    EditorGUILayout.PropertyField(m_LoadSceneTimeOutProperty);
                }

                serializedObject.ApplyModifiedProperties();
            }
        }

        private void DisplayCallToActionButtons()
        {
            if (!m_NetworkManager.IsServer && !m_NetworkManager.IsClient)
            {
                string buttonDisabledReasonSuffix = "";

                if (!EditorApplication.isPlaying)
                {
                    buttonDisabledReasonSuffix = ". This can only be done in play mode";
                    GUI.enabled = false;
                }

                if (m_NetworkManager.NetworkConfig.NetworkTopology == NetworkTopologyTypes.ClientServer)
                {
                    if (GUILayout.Button(new GUIContent("Start Host", "Starts a host instance" + buttonDisabledReasonSuffix)))
                    {
                        m_NetworkManager.StartHost();
                    }

                    if (GUILayout.Button(new GUIContent("Start Server", "Starts a server instance" + buttonDisabledReasonSuffix)))
                    {
                        m_NetworkManager.StartServer();
                    }

                    if (GUILayout.Button(new GUIContent("Start Client", "Starts a client instance" + buttonDisabledReasonSuffix)))
                    {
                        m_NetworkManager.StartClient();
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent("Start Client", "Starts a distributed authority client instance" + buttonDisabledReasonSuffix)))
                    {
                        m_NetworkManager.StartClient();
                    }
                }


                if (!EditorApplication.isPlaying)
                {
                    GUI.enabled = true;
                }
            }
            else
            {
                string instanceType = string.Empty;

                if (m_NetworkManager.IsHost)
                {
                    instanceType = "Host";
                }
                else if (m_NetworkManager.IsServer)
                {
                    instanceType = "Server";
                }
                else if (m_NetworkManager.IsClient)
                {
                    instanceType = "Client";
                }

                EditorGUILayout.HelpBox("You cannot edit the NetworkConfig when a " + instanceType + " is running.", MessageType.Info);

                if (GUILayout.Button(new GUIContent("Stop " + instanceType, "Stops the " + instanceType + " instance.")))
                {
                    m_NetworkManager.Shutdown();
                }
            }
        }

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            var networkManager = target as NetworkManager;
            Initialize();
            CheckNullProperties();
#if !MULTIPLAYER_TOOLS
            DrawInstallMultiplayerToolsTip();
#endif
            void SetExpanded(bool expanded) { networkManager.NetworkManagerExpanded = expanded; };
            DrawFoldOutGroup<NetworkManager>(networkManager.GetType(), DisplayNetworkManagerProperties, networkManager.NetworkManagerExpanded, SetExpanded);
            DisplayCallToActionButtons();
            base.OnInspectorGUI();
        }

        private static void DrawInstallMultiplayerToolsTip()
        {
            const string getToolsText = "Access additional tools for multiplayer development by installing the Multiplayer Tools package in the Package Manager.";
            const string openDocsButtonText = "Open Docs";
            const string dismissButtonText = "Dismiss";
            const string targetUrl = "https://docs-multiplayer.unity3d.com/tools/current/install-tools";
            const string infoIconName = "console.infoicon";

            if (NetcodeForGameObjectsEditorSettings.GetNetcodeInstallMultiplayerToolTips() != 0)
            {
                return;
            }

            if (s_CenteredWordWrappedLabelStyle == null)
            {
                s_CenteredWordWrappedLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };
            }

            if (s_HelpBoxStyle == null)
            {
                s_HelpBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10)
                };
            }

            var openDocsButtonStyle = GUI.skin.button;
            var dismissButtonStyle = EditorStyles.linkLabel;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal(s_HelpBoxStyle, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false), GUILayout.MaxWidth(800));
            {
                GUILayout.Label(new GUIContent(EditorGUIUtility.IconContent(infoIconName)), GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true));
                GUILayout.Space(4);
                GUILayout.Label(getToolsText, s_CenteredWordWrappedLabelStyle, GUILayout.ExpandHeight(true));

                GUILayout.Space(4);

                GUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(openDocsButtonText, openDocsButtonStyle, GUILayout.Width(90), GUILayout.Height(30)))
                {
                    Application.OpenURL(targetUrl);
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();

                GUILayout.Space(4);

                GUILayout.BeginVertical();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(dismissButtonText, dismissButtonStyle, GUILayout.ExpandWidth(false)))
                {
                    NetcodeForGameObjectsEditorSettings.SetNetcodeInstallMultiplayerToolTips(1);
                }
                EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
                GUILayout.FlexibleSpace();
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
        }
    }
}
