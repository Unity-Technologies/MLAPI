using Unity.Netcode.Components;
using UnityEngine;

#if UNITY_EDITOR
using Unity.Netcode.Editor;
using UnityEditor;
/// <summary>
/// The custom editor for the <see cref="MoverScript"/> component.
/// </summary>
[CustomEditor(typeof(PlayerMotion), true)]
public class PlayerMotionEditor : NetworkTransformEditor
{
    private SerializedProperty m_Radius;
    private SerializedProperty m_Speed;

    public override void OnEnable()
    {
        m_Radius = serializedObject.FindProperty(nameof(PlayerMotion.Radius));
        m_Speed = serializedObject.FindProperty(nameof(PlayerMotion.Speed));

        base.OnEnable();
    }

    public override void OnInspectorGUI()
    {
        var playerMotion = target as PlayerMotion;
        playerMotion.ExpandPlayerMotionProperties = EditorGUILayout.BeginFoldoutHeaderGroup(playerMotion.ExpandPlayerMotionProperties, $"{nameof(PlayerMotion)} Properties");
        if (playerMotion.ExpandPlayerMotionProperties)
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.PropertyField(m_Radius);
            EditorGUILayout.PropertyField(m_Speed);
        }
        else
        {
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        EditorGUILayout.Space();

        playerMotion.ExpandNetworkTranspormProperties = EditorGUILayout.BeginFoldoutHeaderGroup(playerMotion.ExpandNetworkTranspormProperties, $"{nameof(NetworkTransform)} Properties");
        if (playerMotion.ExpandNetworkTranspormProperties)
        {
            base.OnInspectorGUI();
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif

public class PlayerMotion : NetworkTransform
{

#if UNITY_EDITOR
    public bool ExpandPlayerMotionProperties;
    public bool ExpandNetworkTranspormProperties;

#endif

    [Range(1.0f, 20.0f)]
    public float Radius = 10.0f;

    [Range(1.0f, 30.0f)]
    public float Speed = 5.0f;

    private float m_CurrentPi;
    private float m_Increment = 0.25f;
    private float m_ClockWise = 1.0f;
    private Rigidbody m_RigidBody;

    public override void OnNetworkSpawn()
    {
        // Always invoked base when deriving from NetworkTransform
        base.OnNetworkSpawn();
        m_RigidBody = GetComponent<Rigidbody>();
        if (CanCommitToTransform)
        {
            m_CurrentPi = Random.Range(-Mathf.PI, Mathf.PI);
            m_ClockWise = Random.Range(-1.0f, 1.0f);
            m_ClockWise = m_ClockWise / Mathf.Abs(m_ClockWise);
            if (!IsOwner)
            {
                Radius += Random.Range(-2.0f, 2.0f);
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsSpawned && CanCommitToTransform)
        {
            m_CurrentPi += m_ClockWise * (Speed * m_Increment * Time.fixedDeltaTime);
            var offset = new Vector3(Radius * Mathf.Cos(m_CurrentPi), transform.position.y, Radius * Mathf.Sin(m_CurrentPi));
            m_RigidBody.MovePosition(Vector3.Lerp(transform.position, offset, Speed * 0.1f * Time.fixedDeltaTime));
        }
    }
}
