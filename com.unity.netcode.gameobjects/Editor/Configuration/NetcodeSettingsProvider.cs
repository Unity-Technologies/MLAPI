using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Directory = UnityEngine.Windows.Directory;
using File = UnityEngine.Windows.File;

namespace Unity.Netcode.Editor.Configuration
{
    internal static class NetcodeSettingsProvider
    {
        private const float k_MaxLabelWidth = 450f;
        private static float s_MaxLabelWidth;
        private static bool s_ShowEditorSettingFields = true;
        private static bool s_ShowProjectSettingFields = true;

        [SettingsProvider]
        public static SettingsProvider CreateNetcodeSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Settings window for the Project scope.
            var provider = new SettingsProvider("Project/NetcodeForGameObjects", SettingsScope.Project)
            {
                label = "Netcode for GameObjects",
                keywords = new[] { "netcode", "editor" },
                guiHandler = OnGuiHandler,
                deactivateHandler = OnDeactivate
            };

            return provider;
        }

        private static void OnDeactivate()
        {
            var settings = NetcodeForGameObjectsProjectSettings.instance;
            if (settings.TempNetworkPrefabsPath != settings.NetworkPrefabsPath)
            {
                var newPath = settings.TempNetworkPrefabsPath;
                if (newPath == "")
                {
                    newPath = NetcodeForGameObjectsProjectSettings.DefaultNetworkPrefabsPath;
                    settings.TempNetworkPrefabsPath = newPath;
                }
                var oldPath = settings.NetworkPrefabsPath;
                settings.NetworkPrefabsPath = settings.TempNetworkPrefabsPath;
                var dirName = Path.GetDirectoryName(newPath);
                if (!Directory.Exists(dirName))
                {
                    var dirs = dirName.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                    var dirsQueue = new Queue<string>(dirs);
                    var parent = dirsQueue.Dequeue();
                    while (dirsQueue.Count != 0)
                    {
                        var child = dirsQueue.Dequeue();
                        var together = Path.Combine(parent, child);
                        if (!Directory.Exists(together))
                        {
                            AssetDatabase.CreateFolder(parent, child);
                        }

                        parent = together;
                    }
                }

                if (Directory.Exists(dirName))
                {
                    if (File.Exists(oldPath))
                    {
                        AssetDatabase.MoveAsset(oldPath, newPath);
                        if (File.Exists(oldPath))
                        {
                            File.Delete(oldPath);
                        }
                        AssetDatabase.Refresh();
                    }
                }
                settings.SaveSettings();
            }
        }


        internal static NetcodeSettingsLabel NetworkObjectsSectionLabel;
        internal static NetcodeSettingsToggle AutoAddNetworkObjectToggle;
        internal static NetcodeSettingsToggle CheckForNetworkObjectToggle;
        internal static NetcodeSettingsLabel MultiplayerToolsLabel;
        internal static NetcodeSettingsToggle MultiplayerToolTipStatusToggle;

        /// <summary>
        /// Creates an instance of the settings UI Elements if they don't already exist.
        /// </summary>
        /// <remarks>
        /// We have to construct any NetcodeGUISettings derived classes here because in
        /// version 2020.x.x EditorStyles.label does not exist yet (higher versions it does)
        /// </remarks>
        private static void CheckForInitialize()
        {
            if (NetworkObjectsSectionLabel == null)
            {
                NetworkObjectsSectionLabel = new NetcodeSettingsLabel("NetworkObject Helper Settings", 20);
            }

            if (AutoAddNetworkObjectToggle == null)
            {
                AutoAddNetworkObjectToggle = new NetcodeSettingsToggle("Auto-Add NetworkObject Component", "When enabled, NetworkObject components are automatically added to GameObjects when NetworkBehaviour components are added first.", 20);
            }

            if (CheckForNetworkObjectToggle == null)
            {
                CheckForNetworkObjectToggle = new NetcodeSettingsToggle("Check for NetworkObject Component", "When disabled, the automatic check on NetworkBehaviours for an associated NetworkObject component will not be performed and Auto-Add NetworkObject Component will be disabled.", 20);
            }

            if (MultiplayerToolsLabel == null)
            {
                MultiplayerToolsLabel = new NetcodeSettingsLabel("Multiplayer Tools", 20);
            }

            if (MultiplayerToolTipStatusToggle == null)
            {
                MultiplayerToolTipStatusToggle = new NetcodeSettingsToggle("Multiplayer Tools Install Reminder", "When enabled, the NetworkManager will display the notification to install the multiplayer tools package.", 20);
            }
        }

        private static void OnGuiHandler(string obj)
        {
            // Make sure all NetcodeGUISettings derived classes are instantiated first
            CheckForInitialize();

            var autoAddNetworkObjectSetting = NetcodeForGameObjectsEditorSettings.GetAutoAddNetworkObjectSetting();
            var checkForNetworkObjectSetting = NetcodeForGameObjectsEditorSettings.GetCheckForNetworkObjectSetting();
            var multiplayerToolsTipStatus = NetcodeForGameObjectsEditorSettings.GetNetcodeInstallMultiplayerToolTips() == 0;
            var settings = NetcodeForGameObjectsProjectSettings.instance;
            var generateDefaultPrefabs = settings.GenerateDefaultNetworkPrefabs;
            var networkPrefabsPath = settings.TempNetworkPrefabsPath;

            EditorGUI.BeginChangeCheck();

            GUILayout.BeginVertical("Box");
            s_ShowEditorSettingFields = EditorGUILayout.BeginFoldoutHeaderGroup(s_ShowEditorSettingFields, "Editor Settings");

            if (s_ShowEditorSettingFields)
            {
                GUILayout.BeginVertical("Box");
                NetworkObjectsSectionLabel.DrawLabel();
                autoAddNetworkObjectSetting = AutoAddNetworkObjectToggle.DrawToggle(autoAddNetworkObjectSetting, checkForNetworkObjectSetting);
                checkForNetworkObjectSetting = CheckForNetworkObjectToggle.DrawToggle(checkForNetworkObjectSetting);
                if (autoAddNetworkObjectSetting && !checkForNetworkObjectSetting)
                {
                    autoAddNetworkObjectSetting = false;
                }
                GUILayout.EndVertical();

                GUILayout.BeginVertical("Box");
                MultiplayerToolsLabel.DrawLabel();
                multiplayerToolsTipStatus = MultiplayerToolTipStatusToggle.DrawToggle(multiplayerToolsTipStatus);
                GUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.EndVertical();

            GUILayout.BeginVertical("Box");
            s_ShowProjectSettingFields = EditorGUILayout.BeginFoldoutHeaderGroup(s_ShowProjectSettingFields, "Project Settings");
            if (s_ShowProjectSettingFields)
            {
                GUILayout.BeginVertical("Box");
                const string generateNetworkPrefabsString = "Generate Default Network Prefabs List";
                const string networkPrefabsLocationString = "Default Network Prefabs List path";

                if (s_MaxLabelWidth == 0)
                {
                    s_MaxLabelWidth = EditorStyles.label.CalcSize(new GUIContent(generateNetworkPrefabsString)).x;
                    s_MaxLabelWidth = Mathf.Min(k_MaxLabelWidth, s_MaxLabelWidth);
                }

                EditorGUIUtility.labelWidth = s_MaxLabelWidth;

                GUILayout.Label("Network Prefabs", EditorStyles.boldLabel);
                generateDefaultPrefabs = EditorGUILayout.Toggle(
                    new GUIContent(
                        generateNetworkPrefabsString,
                        "When enabled, a default NetworkPrefabsList object will be added to your project and kept up " +
                        "to date with all NetworkObject prefabs."),
                    generateDefaultPrefabs,
                    GUILayout.Width(s_MaxLabelWidth + 20));

                GUI.SetNextControlName("Location");
                networkPrefabsPath = EditorGUILayout.TextField(
                    new GUIContent(
                        networkPrefabsLocationString,
                        "The path to the asset the default NetworkPrefabList object should be stored in."),
                    networkPrefabsPath,
                    GUILayout.Width(s_MaxLabelWidth + 270));
                GUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck())
            {
                NetcodeForGameObjectsEditorSettings.SetAutoAddNetworkObjectSetting(autoAddNetworkObjectSetting);
                NetcodeForGameObjectsEditorSettings.SetCheckForNetworkObjectSetting(checkForNetworkObjectSetting);
                NetcodeForGameObjectsEditorSettings.SetNetcodeInstallMultiplayerToolTips(multiplayerToolsTipStatus ? 0 : 1);
                settings.GenerateDefaultNetworkPrefabs = generateDefaultPrefabs;
                settings.TempNetworkPrefabsPath = networkPrefabsPath;
                settings.SaveSettings();
            }
        }
    }

    internal class NetcodeSettingsLabel : NetcodeGUISettings
    {
        private string m_LabelContent;

        public void DrawLabel()
        {
            EditorGUIUtility.labelWidth = m_LabelSize;
            GUILayout.Label(m_LabelContent, EditorStyles.boldLabel, m_LayoutWidth);
        }

        public NetcodeSettingsLabel(string labelText, float layoutOffset = 0.0f)
        {
            m_LabelContent = labelText;
            AdjustLabelSize(labelText, layoutOffset);
        }
    }

    internal class NetcodeSettingsToggle : NetcodeGUISettings
    {
        private GUIContent m_ToggleContent;

        public bool DrawToggle(bool currentSetting, bool enabled = true)
        {
            EditorGUIUtility.labelWidth = m_LabelSize;
            GUI.enabled = enabled;
            var returnValue = EditorGUILayout.Toggle(m_ToggleContent, currentSetting, m_LayoutWidth);
            GUI.enabled = true;
            return returnValue;
        }

        public NetcodeSettingsToggle(string labelText, string toolTip, float layoutOffset)
        {
            AdjustLabelSize(labelText, layoutOffset);
            m_ToggleContent = new GUIContent(labelText, toolTip);
        }
    }

    internal class NetcodeGUISettings
    {
        private const float k_MaxLabelWidth = 450f;
        protected float m_LabelSize { get; private set; }

        protected GUILayoutOption m_LayoutWidth { get; private set; }

        protected void AdjustLabelSize(string labelText, float offset = 0.0f)
        {
            m_LabelSize = Mathf.Min(k_MaxLabelWidth, EditorStyles.label.CalcSize(new GUIContent(labelText)).x);
            m_LayoutWidth = GUILayout.Width(m_LabelSize + offset);
        }
    }

}
