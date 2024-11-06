using System.Collections.Generic;
#if BYPASS_DEFAULT_ENUM_DRAWER && MULTIPLAYER_SERVICES_SDK_INSTALLED
using System.Linq;
#endif
using UnityEditor;
using UnityEngine;

namespace Unity.Netcode.Editor
{
    /// <summary>
    /// The <see cref="CustomEditor"/> for <see cref="NetworkObject"/>
    /// </summary>
    [CustomEditor(typeof(NetworkObject), true)]
    [CanEditMultipleObjects]
    public class NetworkObjectEditor : UnityEditor.Editor
    {
        private bool m_Initialized;
        private NetworkObject m_NetworkObject;
        private bool m_ShowObservers;

        private static readonly string[] k_HiddenFields = { "m_Script" };

        private void Initialize()
        {
            if (m_Initialized)
            {
                return;
            }

            m_Initialized = true;
            m_NetworkObject = (NetworkObject)target;
        }

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            Initialize();

            if (EditorApplication.isPlaying && !m_NetworkObject.IsSpawned && m_NetworkObject.NetworkManager != null && m_NetworkObject.NetworkManager.IsServer)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Spawn", "Spawns the object across the network"));
                if (GUILayout.Toggle(false, "Spawn", EditorStyles.miniButtonLeft))
                {
                    m_NetworkObject.Spawn();
                    EditorUtility.SetDirty(target);
                }

                EditorGUILayout.EndHorizontal();
            }
            else if (EditorApplication.isPlaying && m_NetworkObject.IsSpawned)
            {
                var guiEnabled = GUI.enabled;
                GUI.enabled = false;
                if (m_NetworkObject.NetworkManager.DistributedAuthorityMode)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(NetworkObject.Ownership)));
                }
                EditorGUILayout.TextField(nameof(NetworkObject.GlobalObjectIdHash), m_NetworkObject.GlobalObjectIdHash.ToString());
                EditorGUILayout.TextField(nameof(NetworkObject.NetworkObjectId), m_NetworkObject.NetworkObjectId.ToString());
                EditorGUILayout.TextField(nameof(NetworkObject.OwnerClientId), m_NetworkObject.OwnerClientId.ToString());
                EditorGUILayout.Toggle(nameof(NetworkObject.IsSpawned), m_NetworkObject.IsSpawned);
                EditorGUILayout.Toggle(nameof(NetworkObject.IsLocalPlayer), m_NetworkObject.IsLocalPlayer);
                EditorGUILayout.Toggle(nameof(NetworkObject.IsOwner), m_NetworkObject.IsOwner);
                EditorGUILayout.Toggle(nameof(NetworkObject.IsOwnedByServer), m_NetworkObject.IsOwnedByServer);
                EditorGUILayout.Toggle(nameof(NetworkObject.IsPlayerObject), m_NetworkObject.IsPlayerObject);
                if (m_NetworkObject.IsSceneObject.HasValue)
                {
                    EditorGUILayout.Toggle(nameof(NetworkObject.IsSceneObject), m_NetworkObject.IsSceneObject.Value);
                }
                else
                {
                    EditorGUILayout.TextField(nameof(NetworkObject.IsSceneObject), "null");
                }
                EditorGUILayout.Toggle(nameof(NetworkObject.DestroyWithScene), m_NetworkObject.DestroyWithScene);
                EditorGUILayout.TextField(nameof(NetworkObject.NetworkManager), m_NetworkObject.NetworkManager == null ? "null" : m_NetworkObject.NetworkManager.gameObject.name);
                GUI.enabled = guiEnabled;

                if (m_NetworkObject.NetworkManager != null && m_NetworkObject.NetworkManager.IsServer)
                {
                    m_ShowObservers = EditorGUILayout.Foldout(m_ShowObservers, "Observers");

                    if (m_ShowObservers)
                    {
                        HashSet<ulong>.Enumerator observerClientIds = m_NetworkObject.GetObservers();

                        EditorGUI.indentLevel += 1;

                        while (observerClientIds.MoveNext())
                        {
                            if (m_NetworkObject.NetworkManager.ConnectedClients[observerClientIds.Current].PlayerObject != null)
                            {
                                EditorGUILayout.ObjectField($"ClientId: {observerClientIds.Current}", m_NetworkObject.NetworkManager.ConnectedClients[observerClientIds.Current].PlayerObject, typeof(GameObject), false);
                            }
                            else
                            {
                                EditorGUILayout.TextField($"ClientId: {observerClientIds.Current}", EditorStyles.label);
                            }
                        }

                        EditorGUI.indentLevel -= 1;
                    }
                }
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                serializedObject.UpdateIfRequiredOrScript();
                DrawPropertiesExcluding(serializedObject, k_HiddenFields);
                serializedObject.ApplyModifiedProperties();
                EditorGUI.EndChangeCheck();

                var guiEnabled = GUI.enabled;
                GUI.enabled = false;
                EditorGUILayout.TextField(nameof(NetworkObject.GlobalObjectIdHash), m_NetworkObject.GlobalObjectIdHash.ToString());
                EditorGUILayout.TextField(nameof(NetworkObject.NetworkManager), m_NetworkObject.NetworkManager == null ? "null" : m_NetworkObject.NetworkManager.gameObject.name);
                GUI.enabled = guiEnabled;
            }
        }

        // Saved for use in OnDestroy
        private GameObject m_GameObject;

        /// <summary>
        /// Invoked once when a NetworkObject component is
        /// displayed in the inspector view.
        /// </summary>
        private void OnEnable()
        {
            // We set the GameObject upon being enabled because when the
            // NetworkObject component is removed (i.e. when OnDestroy is invoked)
            // it is no longer valid/available.
            m_GameObject = (target as NetworkObject).gameObject;
        }

        /// <summary>
        /// Invoked once when a NetworkObject component is
        /// no longer displayed in the inspector view.
        /// </summary>
        private void OnDestroy()
        {
            // Since this is also invoked when a NetworkObject component is removed
            // from a GameObject, we go ahead and check for a NetworkObject when
            // this custom editor is destroyed.
            NetworkBehaviourEditor.CheckForNetworkObject(m_GameObject, true);
        }
    }

    // Keeping this here just in case, but it appears that in Unity 6 the visual bugs with
    // enum flags is resolved
#if BYPASS_DEFAULT_ENUM_DRAWER && MULTIPLAYER_SERVICES_SDK_INSTALLED
    [CustomPropertyDrawer(typeof(NetworkObject.OwnershipStatus))]
    public class NetworkObjectOwnership : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            // Don't allow modification while in play mode
            EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);

            // This is a temporary work around due to EditorGUI.EnumFlagsField having a bug in how it displays mask values.
            // For now, we will just display the flags as a toggle and handle the masking of the value ourselves.
            EditorGUILayout.BeginHorizontal();
            var names = System.Enum.GetNames(typeof(NetworkObject.OwnershipStatus)).ToList();
            names.RemoveAt(0);
            var value = property.enumValueFlag;
            var compareValue = 0x01;
            GUILayout.Label(label);
            foreach (var name in names)
            {
                var isSet = (value & compareValue) > 0;
                isSet = GUILayout.Toggle(isSet, name);
                if (isSet)
                {
                    value |= compareValue;
                }
                else
                {
                    value &= ~compareValue;
                }
                compareValue = compareValue << 1;
            }
            property.enumValueFlag = value;
            EditorGUILayout.EndHorizontal();

            // The below can cause visual anomalies and/or throws an exception within the EditorGUI itself (index out of bounds of the array). and has
            // The visual anomaly is when you select one field it is set in the drop down but then the flags selection in the popup menu selects more items
            // even though if you exit the popup menu the flag setting is correct. 
            //var ownership = (NetworkObject.OwnershipStatus)EditorGUI.EnumFlagsField(position, label, (NetworkObject.OwnershipStatus)property.enumValueFlag);
            //property.enumValueFlag = (int)ownership;
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndProperty();
        }
    }
#endif
}
