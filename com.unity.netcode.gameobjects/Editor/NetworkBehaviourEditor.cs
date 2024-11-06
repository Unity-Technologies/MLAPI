using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace Unity.Netcode.Editor
{
    /// <summary>
    /// The <see cref="CustomEditor"/> for <see cref="NetworkBehaviour"/>
    /// </summary>
    [CustomEditor(typeof(NetworkBehaviour), true)]
    [CanEditMultipleObjects]
    public class NetworkBehaviourEditor : UnityEditor.Editor
    {
        private bool m_Initialized;
        private readonly List<string> m_NetworkVariableNames = new List<string>();
        private readonly Dictionary<string, FieldInfo> m_NetworkVariableFields = new Dictionary<string, FieldInfo>();
        private readonly Dictionary<string, object> m_NetworkVariableObjects = new Dictionary<string, object>();

        private GUIContent m_NetworkVariableLabelGuiContent;
        private GUIContent m_NetworkListLabelGuiContent;

        private void Init(MonoScript script)
        {
            m_Initialized = true;

            m_NetworkVariableNames.Clear();
            m_NetworkVariableFields.Clear();
            m_NetworkVariableObjects.Clear();

            m_NetworkVariableLabelGuiContent = new GUIContent("NetworkVariable", "This variable is a NetworkVariable. It can not be serialized and can only be changed during runtime.");
            m_NetworkListLabelGuiContent = new GUIContent("NetworkList", "This variable is a NetworkList. It is rendered, but you can't serialize or change it.");

            var fields = script.GetClass().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < fields.Length; i++)
            {
                var ft = fields[i].FieldType;
                if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(NetworkVariable<>) && !fields[i].IsDefined(typeof(HideInInspector), true) && !fields[i].IsDefined(typeof(NonSerializedAttribute), true))
                {
                    m_NetworkVariableNames.Add(ObjectNames.NicifyVariableName(fields[i].Name));
                    m_NetworkVariableFields.Add(ObjectNames.NicifyVariableName(fields[i].Name), fields[i]);
                }
                if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(NetworkList<>) && !fields[i].IsDefined(typeof(HideInInspector), true) && !fields[i].IsDefined(typeof(NonSerializedAttribute), true))
                {
                    m_NetworkVariableNames.Add(ObjectNames.NicifyVariableName(fields[i].Name));
                    m_NetworkVariableFields.Add(ObjectNames.NicifyVariableName(fields[i].Name), fields[i]);
                }
            }
        }

        private void RenderNetworkVariable(int index)
        {
            if (!m_NetworkVariableFields.ContainsKey(m_NetworkVariableNames[index]))
            {
                serializedObject.Update();
                var scriptProperty = serializedObject.FindProperty("m_Script");
                if (scriptProperty == null)
                {
                    return;
                }

                var targetScript = scriptProperty.objectReferenceValue as MonoScript;
                Init(targetScript);
            }

            object value = m_NetworkVariableFields[m_NetworkVariableNames[index]].GetValue(target);
            if (value == null)
            {
                var fieldType = m_NetworkVariableFields[m_NetworkVariableNames[index]].FieldType;
                var networkVariable = (NetworkVariableBase)Activator.CreateInstance(fieldType, true);
                m_NetworkVariableFields[m_NetworkVariableNames[index]].SetValue(target, networkVariable);
            }

            var type = m_NetworkVariableFields[m_NetworkVariableNames[index]].GetValue(target).GetType();
            var genericType = type.GetGenericArguments()[0];

            EditorGUILayout.BeginHorizontal();
            if (genericType.IsValueType)
            {
                var isEquatable = false;
                foreach (var iface in genericType.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEquatable<>))
                    {
                        isEquatable = true;
                    }
                }

                MethodInfo method;
                if (isEquatable)
                {
                    method = typeof(NetworkBehaviourEditor).GetMethod(nameof(RenderNetworkContainerValueTypeIEquatable), BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic);
                }
                else
                {
                    method = typeof(NetworkBehaviourEditor).GetMethod(nameof(RenderNetworkContainerValueType), BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.NonPublic);
                }

                var genericMethod = method.MakeGenericMethod(genericType);
                genericMethod.Invoke(this, new[] { (object)index });
            }
            else
            {
                EditorGUILayout.LabelField("Type not renderable");

                GUILayout.Label(m_NetworkVariableLabelGuiContent, EditorStyles.miniLabel, GUILayout.Width(EditorStyles.miniLabel.CalcSize(m_NetworkVariableLabelGuiContent).x));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void RenderNetworkContainerValueType<T>(int index) where T : unmanaged
        {
            try
            {
                var networkVariable = (NetworkVariable<T>)m_NetworkVariableFields[m_NetworkVariableNames[index]].GetValue(target);
                RenderNetworkVariableValueType(index, networkVariable);
            }
            catch (Exception e)
            {
                Debug.Log(e);
                throw;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RenderNetworkContainerValueTypeIEquatable<T>(int index) where T : unmanaged, IEquatable<T>
        {
            try
            {
                var networkVariable = (NetworkVariable<T>)m_NetworkVariableFields[m_NetworkVariableNames[index]].GetValue(target);
                RenderNetworkVariableValueType(index, networkVariable);
            }
            catch (Exception)
            {
                try
                {
                    var networkList = (NetworkList<T>)m_NetworkVariableFields[m_NetworkVariableNames[index]].GetValue(target);
                    RenderNetworkListValueType(index, networkList);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                    throw;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void RenderNetworkVariableValueType<T>(int index, NetworkVariable<T> networkVariable) where T : unmanaged
        {
            var type = typeof(T);
            object val = networkVariable.Value;
            string variableName = m_NetworkVariableNames[index];

            var behaviour = (NetworkBehaviour)target;

            // Only server can MODIFY. So allow modification if network is either not running or we are server
            if (behaviour.IsBehaviourEditable())
            {
                if (type == typeof(int))
                {
                    val = EditorGUILayout.IntField(variableName, (int)val);
                }
                else if (type == typeof(uint))
                {
                    val = (uint)EditorGUILayout.LongField(variableName, (uint)val);
                }
                else if (type == typeof(short))
                {
                    val = (short)EditorGUILayout.IntField(variableName, (short)val);
                }
                else if (type == typeof(ushort))
                {
                    val = (ushort)EditorGUILayout.IntField(variableName, (ushort)val);
                }
                else if (type == typeof(sbyte))
                {
                    val = (sbyte)EditorGUILayout.IntField(variableName, (sbyte)val);
                }
                else if (type == typeof(byte))
                {
                    val = (byte)EditorGUILayout.IntField(variableName, (byte)val);
                }
                else if (type == typeof(long))
                {
                    val = EditorGUILayout.LongField(variableName, (long)val);
                }
                else if (type == typeof(ulong))
                {
                    val = (ulong)EditorGUILayout.LongField(variableName, (long)((ulong)val));
                }
                else if (type == typeof(float))
                {
                    val = EditorGUILayout.FloatField(variableName, (float)((float)val));
                }
                else if (type == typeof(bool))
                {
                    val = EditorGUILayout.Toggle(variableName, (bool)val);
                }
                else if (type == typeof(string))
                {
                    val = EditorGUILayout.TextField(variableName, (string)val);
                }
                else if (type.IsEnum)
                {
                    val = EditorGUILayout.EnumPopup(variableName, (Enum)val);
                }
                else
                {
                    EditorGUILayout.LabelField("Type not renderable");
                }

                networkVariable.Value = (T)val;
            }
            else
            {
                EditorGUILayout.LabelField(variableName, EditorStyles.wordWrappedLabel);
                EditorGUILayout.SelectableLabel(val.ToString(), EditorStyles.wordWrappedLabel);
            }
            GUILayout.Label(m_NetworkVariableLabelGuiContent, EditorStyles.miniLabel, GUILayout.Width(EditorStyles.miniLabel.CalcSize(m_NetworkVariableLabelGuiContent).x));
        }

        private void RenderNetworkListValueType<T>(int index, NetworkList<T> networkList)
            where T : unmanaged, IEquatable<T>
        {
            string variableName = m_NetworkVariableNames[index];

            string value = "";
            bool addComma = false;
            foreach (var v in networkList)
            {
                if (addComma)
                {
                    value += ", ";
                }
                value += v.ToString();
                addComma = true;
            }
            EditorGUILayout.LabelField(variableName, value);
            GUILayout.Label(m_NetworkListLabelGuiContent, EditorStyles.miniLabel, GUILayout.Width(EditorStyles.miniLabel.CalcSize(m_NetworkListLabelGuiContent).x));
        }

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            if (!m_Initialized)
            {
                serializedObject.Update();
                var scriptProperty = serializedObject.FindProperty("m_Script");
                if (scriptProperty == null)
                {
                    return;
                }

                var targetScript = scriptProperty.objectReferenceValue as MonoScript;
                Init(targetScript);
            }

            EditorGUI.BeginChangeCheck();
            serializedObject.Update();

            for (int i = 0; i < m_NetworkVariableNames.Count; i++)
            {
                RenderNetworkVariable(i);
            }

            var property = serializedObject.GetIterator();
            bool expanded = true;
            while (property.NextVisible(expanded))
            {
                if (m_NetworkVariableNames.Contains(ObjectNames.NicifyVariableName(property.name)))
                {
                    // Skip rendering of NetworkVars, they have special rendering
                    continue;
                }

                if (property.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (property.name == "m_Script")
                    {
                        EditorGUI.BeginDisabledGroup(true);
                    }

                    EditorGUILayout.PropertyField(property, true);

                    if (property.name == "m_Script")
                    {
                        EditorGUI.EndDisabledGroup();
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(property, true);
                    EditorGUILayout.EndHorizontal();
                }

                expanded = false;
            }
            EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Invoked once when a NetworkBehaviour component is
        /// displayed in the inspector view.
        /// </summary>
        private void OnEnable()
        {
            // This can be null and throw an exception when running test runner in the editor
            if (target == null)
            {
                return;
            }
            // When we first add a NetworkBehaviour this editor will be enabled
            // so we go ahead and check for an already existing NetworkObject here
            CheckForNetworkObject((target as NetworkBehaviour).gameObject);
        }

        /// <summary>
        /// Recursively finds the root parent of a <see cref="Transform"/>
        /// </summary>
        /// <param name="transform">The current <see cref="Transform"/> we are inspecting for a parent</param>
        /// <returns>the root parent for the first <see cref="Transform"/> passed into the method</returns>
        public static Transform GetRootParentTransform(Transform transform)
        {
            if (transform.parent == null || transform.parent == transform)
            {
                return transform;
            }
            return GetRootParentTransform(transform.parent);
        }

        /// <summary>
        /// Used to determine if a GameObject has one or more NetworkBehaviours but
        /// does not already have a NetworkObject component.  If not it will notify
        /// the user that NetworkBehaviours require a NetworkObject.
        /// </summary>
        /// <param name="gameObject"><see cref="GameObject"/> to start checking for a <see cref="NetworkObject"/></param>
        /// <param name="networkObjectRemoved">used internally</param>
        public static void CheckForNetworkObject(GameObject gameObject, bool networkObjectRemoved = false)
        {
            // If there are no NetworkBehaviours or gameObjects then exit early
            // If we are in play mode and a user is inspecting something then exit early (we don't add NetworkObjects to something when in play mode)
            if (EditorApplication.isPlaying || gameObject == null || (gameObject.GetComponent<NetworkBehaviour>() == null && gameObject.GetComponentInChildren<NetworkBehaviour>() == null))
            {
                return;
            }

            // If this automatic check is disabled, then do not perform this check.
            if (!NetcodeForGameObjectsEditorSettings.GetCheckForNetworkObjectSetting())
            {
                return;
            }

            // Now get the root parent transform to the current GameObject (or itself)
            var rootTransform = GetRootParentTransform(gameObject.transform);
            if (!rootTransform.TryGetComponent<NetworkManager>(out var networkManager))
            {
                networkManager = rootTransform.GetComponentInChildren<NetworkManager>();
            }

            // If there is a NetworkManager, then notify the user that a NetworkManager cannot have NetworkBehaviour components
            if (networkManager != null)
            {
                var networkBehaviours = networkManager.gameObject.GetComponents<NetworkBehaviour>();
                var networkBehavioursChildren = networkManager.gameObject.GetComponentsInChildren<NetworkBehaviour>();
                if (networkBehaviours.Length > 0 || networkBehavioursChildren.Length > 0)
                {
                    if (EditorUtility.DisplayDialog("NetworkBehaviour or NetworkManager Cannot Be Added", $"{nameof(NetworkManager)}s cannot have {nameof(NetworkBehaviour)} components added to the root parent or any of its children." +
                        $" Would you like to remove the NetworkManager or NetworkBehaviour?", "NetworkManager", "NetworkBehaviour"))
                    {
                        DestroyImmediate(networkManager);
                    }
                    else
                    {
                        foreach (var networkBehaviour in networkBehaviours)
                        {
                            DestroyImmediate(networkBehaviour);
                        }

                        foreach (var networkBehaviour in networkBehaviours)
                        {
                            DestroyImmediate(networkBehaviour);
                        }
                    }
                    return;
                }
            }

            // Otherwise, check to see if there is any NetworkObject from the root GameObject down to all children.
            // If not, notify the user that NetworkBehaviours require that the relative GameObject has a NetworkObject component.
            if (!rootTransform.TryGetComponent<NetworkObject>(out var networkObject))
            {
                networkObject = rootTransform.GetComponentInChildren<NetworkObject>();

                if (networkObject == null)
                {
                    // If we are removing a NetworkObject but there is still one or more NetworkBehaviour components
                    // and the user has already turned "Auto-Add NetworkObject" on when first notified about the requirement
                    // then just send a reminder to the user why the NetworkObject they just deleted seemingly "re-appeared"
                    // again.
                    if (networkObjectRemoved && NetcodeForGameObjectsEditorSettings.GetAutoAddNetworkObjectSetting())
                    {
                        Debug.LogWarning($"{gameObject.name} still has {nameof(NetworkBehaviour)}s and Auto-Add NetworkObjects is enabled. A NetworkObject is being added back to {gameObject.name}.");
                        Debug.Log($"To reset Auto-Add NetworkObjects: Select the Netcode->General->Reset Auto-Add NetworkObject menu item.");
                    }

                    // Notify and provide the option to add it one time, always add a NetworkObject, or do nothing and let the user manually add it
                    if (EditorUtility.DisplayDialog($"{nameof(NetworkBehaviour)}s require a {nameof(NetworkObject)}",
                    $"{gameObject.name} does not have a {nameof(NetworkObject)} component.  Would you like to add one now?", "Yes", "No (manually add it)",
                    DialogOptOutDecisionType.ForThisMachine, NetcodeForGameObjectsEditorSettings.AutoAddNetworkObjectIfNoneExists))
                    {
                        gameObject.AddComponent<NetworkObject>();
                        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);
                    }
                }
            }

            if (networkObject != null)
            {
                OrderNetworkObject(networkObject);
            }
        }

        // Assures the NetworkObject precedes any NetworkBehaviour on the same GameObject as the NetworkObject
        private static void OrderNetworkObject(NetworkObject networkObject)
        {
            var monoBehaviours = networkObject.gameObject.GetComponents<MonoBehaviour>();
            var networkObjectIndex = 0;
            var firstNetworkBehaviourIndex = -1;
            for (int i = 0; i < monoBehaviours.Length; i++)
            {
                if (monoBehaviours[i] == networkObject)
                {
                    networkObjectIndex = i;
                    break;
                }

                var networkBehaviour = monoBehaviours[i] as NetworkBehaviour;
                if (networkBehaviour != null)
                {
                    // Get the index of the first NetworkBehaviour Component
                    if (firstNetworkBehaviourIndex == -1)
                    {
                        firstNetworkBehaviourIndex = i;
                    }
                }
            }

            if (firstNetworkBehaviourIndex != -1 && networkObjectIndex > firstNetworkBehaviourIndex)
            {
                var positionsToMove = networkObjectIndex - firstNetworkBehaviourIndex;
                for (int i = 0; i < positionsToMove; i++)
                {
                    UnityEditorInternal.ComponentUtility.MoveComponentUp(networkObject);
                }

                EditorUtility.SetDirty(networkObject.gameObject);
            }
        }
    }
}
