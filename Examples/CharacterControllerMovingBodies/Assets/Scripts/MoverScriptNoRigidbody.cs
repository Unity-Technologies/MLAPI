using UnityEngine;
using Unity.Netcode.Components;
using Unity.Netcode;
#if UNITY_EDITOR
using Unity.Netcode.Editor;
using UnityEditor;

/// <summary>
/// The custom editor for the <see cref="MoverScriptNoRigidbody"/> component.
/// </summary>
[CustomEditor(typeof(MoverScriptNoRigidbody), true)]
[CanEditMultipleObjects]
public class MoverScriptNoRigidbodyEditor : NetworkTransformEditor
{
    private SerializedProperty m_Radius;
    private SerializedProperty m_Increment;
    private SerializedProperty m_RotateSpeed;
    private SerializedProperty m_MovementSpeed;
    private SerializedProperty m_AirSpeedFactor;
    private SerializedProperty m_Gravity;
    private SerializedProperty m_ContinualChildMotion;


    public override void OnEnable()
    {
        m_Radius = serializedObject.FindProperty(nameof(MoverScriptNoRigidbody.SpawnRadius));
        m_Increment = serializedObject.FindProperty(nameof(MoverScriptNoRigidbody.Increment));
        m_RotateSpeed = serializedObject.FindProperty(nameof(MoverScriptNoRigidbody.RotationSpeed));
        m_MovementSpeed = serializedObject.FindProperty(nameof(MoverScriptNoRigidbody.MovementSpeed));
        m_AirSpeedFactor = serializedObject.FindProperty(nameof(MoverScriptNoRigidbody.AirSpeedFactor));
        m_Gravity = serializedObject.FindProperty(nameof(MoverScriptNoRigidbody.Gravity));
        m_ContinualChildMotion = serializedObject.FindProperty(nameof(MoverScriptNoRigidbody.ContinualChildMotion));

        base.OnEnable();
    }

    private void DisplayerMoverScriptNoRigidbodyProperties()
    {
        EditorGUILayout.PropertyField(m_Radius);
        EditorGUILayout.PropertyField(m_Increment);
        EditorGUILayout.PropertyField(m_RotateSpeed);
        EditorGUILayout.PropertyField(m_MovementSpeed);
        EditorGUILayout.PropertyField(m_AirSpeedFactor);
        EditorGUILayout.PropertyField(m_Gravity);
        EditorGUILayout.PropertyField(m_ContinualChildMotion);
    }

    public override void OnInspectorGUI()
    {
        var moverScriptNoRigidbody = target as MoverScriptNoRigidbody;
        void SetExpanded(bool expanded) { moverScriptNoRigidbody.MoverScriptNoRigidbodyExpanded = expanded; };
        DrawFoldOutGroup<MoverScriptNoRigidbody>(moverScriptNoRigidbody.GetType(), DisplayerMoverScriptNoRigidbodyProperties, moverScriptNoRigidbody.MoverScriptNoRigidbodyExpanded, SetExpanded);
        base.OnInspectorGUI();
    }
}
#endif

/// <summary>
/// The player controller for the player prefab
/// </summary>
public class MoverScriptNoRigidbody : NetworkTransform
{
#if UNITY_EDITOR
    // Inspector view expand/collapse settings for this derived child class
    [HideInInspector]
    public bool MoverScriptNoRigidbodyExpanded;
#endif

    private static bool s_EnablePlayerParentingText = true;

    [Tooltip("Radius range a player will spawn within.")]
    [Range(1.0f, 40.0f)]
    public float SpawnRadius = 10.0f;

    [Range(0.001f, 10.0f)]
    public float Increment = 1.0f;

    [Tooltip("The rotation speed multiplier.")]
    [Range(0.01f, 2.0f)]
    public float RotationSpeed = 1.0f;

    [Tooltip("The forward movement speed.")]
    [Range(0.01f, 30.0f)]
    public float MovementSpeed = 15.0f;

    [Tooltip("The jump launching speed.")]
    [Range(1.0f, 20f)]
    public float JumpSpeed = 10.0f;

    [Tooltip("Determines how much the player's motion is applied when in the air.")]
    [Range(0.01f, 1.0f)]
    public float AirSpeedFactor = 0.35f;

    [Range(-20.0f, 20.0f)]
    public float Gravity = -9.8f;

    [Tooltip("When enabled, the child spheres will continually move. When disabled, the child spheres will only move when the player moves.")]
    public bool ContinualChildMotion = true;


    private TextMesh m_ParentedText;
    private PlayerColor m_PlayerColor;
    private float m_JumpDelay;
    private Vector3 m_WorldMotion = Vector3.zero;
    private Vector3 m_CameraOriginalPosition;
    private Quaternion m_CameraOriginalRotation;
    private CharacterController m_CharacterController;
    private PlayerBallMotion m_PlayerBallMotion;

    protected override void Awake()
    {
        m_ParentedText = GetComponentInChildren<TextMesh>();
        m_ParentedText?.gameObject.SetActive(false);
        m_PlayerColor = GetComponent<PlayerColor>();
        m_PlayerBallMotion = GetComponentInChildren<PlayerBallMotion>();
        base.Awake();
    }

    /// <summary>
    /// Invoked after being instantiated, we can do other pre-spawn related
    /// initilization tasks here.
    /// </summary>
    /// <remarks>
    /// This provides you with a reference to the current <see cref="NetworkManager"/>
    /// since that is not set on the <see cref="NetworkBehaviour"/> until it is spawned.
    /// </remarks>
    /// <param name="networkManager"></param>
    protected override void OnNetworkPreSpawn(ref NetworkManager networkManager)
    {
        m_CharacterController = GetComponent<CharacterController>();
        // By default, we always disable the CharacterController and only enable it on the
        // owner/authority side.
        m_CharacterController.enabled = false;
        base.OnNetworkPreSpawn(ref networkManager);
    }

    /// <summary>
    /// We are using post spawn to handle any final spawn initializations.
    /// At this point we know all NetworkBehaviours on this instance has 
    /// been spawned.
    /// </summary>
    protected override void OnNetworkPostSpawn()
    {
        m_CharacterController.enabled = CanCommitToTransform;
        if (CanCommitToTransform)
        {
            m_PlayerBallMotion.SetContinualMotion(ContinualChildMotion);
            Random.InitState((int)System.DateTime.Now.Ticks);
            transform.position += new Vector3(Random.Range(-SpawnRadius, SpawnRadius), 1.25f, Random.Range(0, SpawnRadius));
            SetState(transform.position, null, null, false);
            if (IsLocalPlayer)
            {
                NetworkObject.DontDestroyWithOwner = false;
                m_CameraOriginalPosition = Camera.main.transform.position;
                m_CameraOriginalRotation = Camera.main.transform.rotation;
                Camera.main.transform.SetParent(transform, false);
            }
        }

        if (NetworkObject.IsPlayerObject)
        {
            gameObject.name = $"Player-{OwnerClientId}";
        }

        m_ParentedText?.gameObject.SetActive(true);
        UpdateParentedText();
        base.OnNetworkPostSpawn();
    }

    public override void OnNetworkDespawn()
    {
        if (IsLocalPlayer)
        {
            m_CharacterController.enabled = false;
            Camera.main.transform.SetParent(null, false);
            Camera.main.transform.position = m_CameraOriginalPosition;
            Camera.main.transform.rotation = m_CameraOriginalRotation;
        }
        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Bypass NetworkTransform's OnNetworkObjectParentChanged
    /// </summary>
    public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
    {
        if (parentNetworkObject != null)
        {
            Debug.Log($"Parented under {parentNetworkObject.name}");
        }
        UpdateParentedText();
        base.OnNetworkObjectParentChanged(parentNetworkObject);
    }

    /// <summary>
    /// This method handles both client-server and distributed authority network topologies
    /// client-server: If we are not the server, then we need to send an Rpc to the server to handle parenting since the Character controller is disabled on the server for all client CharacterControllers (i.e. won't trigger).
    /// distributed authority: If we are the authority, then handle parenting locally.
    /// </summary>
    /// <param name="parent"></param>
    public void SetParent(NetworkObject parent)
    {
        if ((!NetworkManager.DistributedAuthorityMode && (IsServer || (NetworkObject.AllowOwnerToParent && IsOwner))) || (NetworkManager.DistributedAuthorityMode && HasAuthority))
        {
            if (parent != null)
            {
                NetworkObject.TrySetParent(parent);
            }
            else
            {
                NetworkObject.TryRemoveParent();
            }
        }
        else if (!NetworkManager.DistributedAuthorityMode && !IsServer)
        {
            SetParentRpc(new NetworkObjectReference(parent));
        }
    }

    [Rpc(SendTo.Server)]
    public void SetParentRpc(NetworkObjectReference parentReference, RpcParams rpcParams = default)
    {
        var parent = (NetworkObject)null;
        parentReference.TryGet(out parent, NetworkManager);
        if (parent != null)
        {
            NetworkObject.TrySetParent(parent);
        }
        else
        {
            NetworkObject.TryRemoveParent();
        }
    }


    private void Update()
    {
        if (!IsSpawned || !CanCommitToTransform)
        {
            return;
        }
        ApplyInput();
    }


    private Vector3 m_PushMotion = Vector3.zero;
    /// <summary>
    /// Since <see cref="CharacterController"/> has issues with collisions and rotating bodies,
    /// we have to simulate the collision using triggers.
    /// </summary>
    /// <remarks>
    /// <see cref="TriggerPush"/>
    /// </remarks>
    /// <param name="normal">direction to push away from</param>
    public void PushAwayFrom(Vector3 normal)
    {
        m_PushMotion += normal * MovementSpeed * 0.10f * Time.deltaTime;
    }

    /// <summary>
    /// Handles player input
    /// </summary>
    private void ApplyInput()
    {
        // Simple rotation:
        // Since the forward vector is perpendicular to the right vector of the player, we can just
        // apply the +/- value to our forward direction and lerp our right vector towards that direction
        // in order to get a reasonably smooth rotation.
        var rotation = transform.forward;
        m_WorldMotion = Vector3.Lerp(m_WorldMotion, m_CharacterController.isGrounded ? Vector3.zero : Vector3.up * Gravity, Time.deltaTime * 2f);
        var motion = m_WorldMotion * Time.deltaTime + m_PushMotion;
        var moveMotion = 0.0f;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            motion += transform.forward * MovementSpeed * Time.deltaTime * (m_CharacterController.isGrounded ? 1.0f : AirSpeedFactor);
            moveMotion = 1.0f;
            m_CharacterController.Move(motion);
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            motion += (transform.forward * -MovementSpeed) * Time.deltaTime * (m_CharacterController.isGrounded ? 1.0f : AirSpeedFactor);
            moveMotion = -1.0f;
            m_CharacterController.Move(motion);
        }

        if (!m_CharacterController.isGrounded || m_JumpDelay > Time.realtimeSinceStartup || m_PushMotion.magnitude > 0.01f)
        {
            m_CharacterController.Move(motion);
        }

        if (Input.GetKeyDown(KeyCode.Space) && m_CharacterController.isGrounded)
        {
            m_JumpDelay = Time.realtimeSinceStartup + 0.5f;
            m_WorldMotion = motion + Vector3.up * JumpSpeed;
        }

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            transform.right = Vector3.Lerp(transform.right, rotation * RotationSpeed, Time.deltaTime).normalized;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            transform.right = Vector3.Lerp(transform.right, rotation * -RotationSpeed, Time.deltaTime).normalized;
        }

        // Enabled/Disable player name, transform space, and parent TextMesh
        if (Input.GetKeyDown(KeyCode.P))
        {
            s_EnablePlayerParentingText = !s_EnablePlayerParentingText;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ContinualChildMotion = !ContinualChildMotion;
            m_PlayerBallMotion.SetContinualMotion(ContinualChildMotion);
        }

        m_PushMotion = Vector3.Lerp(m_PushMotion, Vector3.zero, 0.35f);

        m_PlayerBallMotion.HasMotion(moveMotion);
    }

    /// <summary>
    /// Updates player TextMesh relative to each client's camera view
    /// </summary>
    private void OnGUI()
    {
        if (m_ParentedText != null)
        {
            if (m_ParentedText.gameObject.activeInHierarchy != s_EnablePlayerParentingText)
            {
                m_ParentedText.gameObject.SetActive(s_EnablePlayerParentingText);
            }
            if (s_EnablePlayerParentingText)
            {
                var position = Camera.main.transform.position;
                position.y = m_ParentedText.transform.position.y;
                m_ParentedText.transform.LookAt(position, transform.up);
                m_ParentedText.transform.forward = -m_ParentedText.transform.forward;
            }
        }
    }

    /// <summary>
    /// Updates the contents of the parented <see cref="TextMesh"/>
    /// </summary>
    private void UpdateParentedText()
    {
        if (m_ParentedText)
        {
            m_ParentedText.color = m_PlayerColor.Color;
            if (transform.parent)
            {
                m_ParentedText.text = $"{gameObject.name}\n Local Space\n Parent: {transform.parent.name}";
            }
            else
            {
                m_ParentedText.text = $"{gameObject.name}\n WorldSpace\n Parent: None";
            }
        }
    }
}
