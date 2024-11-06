#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
using Unity.Netcode.Components;
using UnityEditor;

namespace Unity.Netcode.Editor
{
    [CustomEditor(typeof(NetworkRigidbodyBase), true)]
    [CanEditMultipleObjects]
    public class NetworkRigidbodyBaseEditor : NetcodeEditorBase<NetworkBehaviour>
    {
        private SerializedProperty m_UseRigidBodyForMotion;
        private SerializedProperty m_AutoUpdateKinematicState;
        private SerializedProperty m_AutoSetKinematicOnDespawn;


        public override void OnEnable()
        {
            m_UseRigidBodyForMotion = serializedObject.FindProperty(nameof(NetworkRigidbodyBase.UseRigidBodyForMotion));
            m_AutoUpdateKinematicState = serializedObject.FindProperty(nameof(NetworkRigidbodyBase.AutoUpdateKinematicState));
            m_AutoSetKinematicOnDespawn = serializedObject.FindProperty(nameof(NetworkRigidbodyBase.AutoSetKinematicOnDespawn));

            base.OnEnable();
        }

        private void DisplayNetworkRigidbodyProperties()
        {
            EditorGUILayout.PropertyField(m_UseRigidBodyForMotion);
            EditorGUILayout.PropertyField(m_AutoUpdateKinematicState);
            EditorGUILayout.PropertyField(m_AutoSetKinematicOnDespawn);
        }

        /// <inheritdoc/>
        public override void OnInspectorGUI()
        {
            var networkRigidbodyBase = target as NetworkRigidbodyBase;
            void SetExpanded(bool expanded) { networkRigidbodyBase.NetworkRigidbodyBaseExpanded = expanded; };
            DrawFoldOutGroup<NetworkRigidbodyBase>(networkRigidbodyBase.GetType(), DisplayNetworkRigidbodyProperties, networkRigidbodyBase.NetworkRigidbodyBaseExpanded, SetExpanded);
            base.OnInspectorGUI();
        }
    }
}
#endif
