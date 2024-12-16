using System.Collections.Generic;
using Unity.Netcode.Components;
using UnityEngine;
using System.Linq;


#if UNITY_EDITOR
using Unity.Netcode.Editor;
using UnityEditor;

/// <summary>
/// The custom editor for the <see cref="MoverScriptNoRigidbody"/> component.
/// </summary>
[CustomEditor(typeof(PlayerBallMotion), true)]
[CanEditMultipleObjects]
public class PlayerBallMotionEditor : NetworkTransformEditor
{
    private SerializedProperty m_RotationAxis;
    private SerializedProperty m_RotationSpeed;
    

    public override void OnEnable()
    {
        m_RotationAxis = serializedObject.FindProperty(nameof(PlayerBallMotion.RotationAxis));
        m_RotationSpeed = serializedObject.FindProperty(nameof(PlayerBallMotion.RotationSpeed));
        base.OnEnable();
    }

    private void DrawPlayerBallMotionProperties()
    {
        EditorGUILayout.PropertyField(m_RotationAxis);
        EditorGUILayout.PropertyField(m_RotationSpeed);
    }

    public override void OnInspectorGUI()
    {
        var playerBallMotion = target as PlayerBallMotion;
        void SetExpanded(bool expanded) { playerBallMotion.ExpandPlayerBallMotion = expanded; };
        DrawFoldOutGroup< PlayerBallMotion>(playerBallMotion.GetType(), DrawPlayerBallMotionProperties, playerBallMotion.ExpandPlayerBallMotion, SetExpanded);
        base.OnInspectorGUI();
    }
}
#endif

public class PlayerBallMotion : NetworkTransform
{
#if UNITY_EDITOR
    public bool ExpandPlayerBallMotion;
    public bool ExpandNetworkTransform;
#endif
    public enum RotateAroundAxis
    {
        Up,
        Right,
        Forward
    }

    public RotateAroundAxis RotationAxis;
    public float RotationSpeed = 1.5f;

    private Vector3 m_AxisRotation = Vector3.zero;
    private List<PlayerBallMotion> m_Children;

    private bool m_ContinualMotion;
    private float m_CurrentRotionMotion = 1.0f;
    public void SetContinualMotion(bool continualMotion)
    {
        m_ContinualMotion = continualMotion;
        foreach (var child in m_Children)
        {
            child.SetContinualMotion(continualMotion);
        }
    }

    protected override void Awake()
    {
        m_Children = GetComponentsInChildren<PlayerBallMotion>().Where((c)=> c != this).ToList();
        base.Awake();
    }

    private void SetRotationAixs()
    {
        switch (RotationAxis)
        {
            case RotateAroundAxis.Up:
                {
                    m_AxisRotation = transform.parent.up;
                    break;
                }
            case RotateAroundAxis.Right:
                {
                    m_AxisRotation = transform.parent.right;
                    break;
                }
            case RotateAroundAxis.Forward:
                {
                    m_AxisRotation = transform.parent.forward;
                    break;
                }
        }
    }

    public void HasMotion(float direction)
    {
        if (direction == 0.0f)
        {
            if(!m_ContinualMotion)
            {
                return;
            }
        }
        else
        {
            m_CurrentRotionMotion = RotationSpeed * direction;
        }


        transform.LookAt(transform.parent);
        SetRotationAixs();
        transform.RotateAround(transform.parent.position, m_AxisRotation, m_CurrentRotionMotion);
        foreach(var child in m_Children)
        {
            child.HasMotion(direction);
        }
    }
}
