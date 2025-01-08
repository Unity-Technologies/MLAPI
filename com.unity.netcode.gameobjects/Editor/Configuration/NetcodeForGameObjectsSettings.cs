using UnityEditor;

namespace Unity.Netcode.Editor.Configuration
{
    internal class NetcodeForGameObjectsEditorSettings
    {
        internal const string AutoAddNetworkObjectIfNoneExists = "AutoAdd-NetworkObject-When-None-Exist";
        internal const string CheckForNetworkObject = "NetworkBehaviour-Check-For-NetworkObject";
        internal const string InstallMultiplayerToolsTipDismissedPlayerPrefKey = "Netcode_Tip_InstallMPTools_Dismissed";

        internal static int GetNetcodeInstallMultiplayerToolTips()
        {
            if (EditorPrefs.HasKey(InstallMultiplayerToolsTipDismissedPlayerPrefKey))
            {
                return EditorPrefs.GetInt(InstallMultiplayerToolsTipDismissedPlayerPrefKey);
            }

            return 0;
        }

        internal static void SetNetcodeInstallMultiplayerToolTips(int toolTipPrefSetting)
        {
            EditorPrefs.SetInt(InstallMultiplayerToolsTipDismissedPlayerPrefKey, toolTipPrefSetting);
        }

        internal static bool GetAutoAddNetworkObjectSetting()
        {
            if (EditorPrefs.HasKey(AutoAddNetworkObjectIfNoneExists))
            {
                return EditorPrefs.GetBool(AutoAddNetworkObjectIfNoneExists);
            }
            // Default for this is false
            return false;
        }

        internal static void SetAutoAddNetworkObjectSetting(bool autoAddSetting)
        {
            EditorPrefs.SetBool(AutoAddNetworkObjectIfNoneExists, autoAddSetting);
        }

        internal static bool GetCheckForNetworkObjectSetting()
        {
            if (EditorPrefs.HasKey(CheckForNetworkObject))
            {
                return EditorPrefs.GetBool(CheckForNetworkObject);
            }
            // Default for this is true
            return true;
        }

        internal static void SetCheckForNetworkObjectSetting(bool checkForNetworkObject)
        {
            EditorPrefs.SetBool(CheckForNetworkObject, checkForNetworkObject);
        }
    }
}
