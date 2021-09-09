using UnityEngine;
using Unity.Netcode;

/// <summary>
/// A general object used for pooling purposes
/// </summary>
public class GenericPooledObjectBehaviour : NetworkBehaviour
{
    [SerializeField]
    [Tooltip("This will make the spawned objects move around randomly.  !Caution! You can generate a lot of objects this way!")]
    private bool m_MoveRandomly = true;

    [HideInInspector]
    public bool HasHandler;

    [HideInInspector]
    public int SyncrhonizedObjectTypeIndex;

    private Rigidbody m_RigidBody;
    private MeshRenderer m_MeshRenderer;
    private Vector3 m_Direction;
    private float m_Velocity;
    private float m_VisibilitySpawn;
    private bool m_ShouldDespawn;

    [HideInInspector]
    public bool MovementEnabled;

    private void Start()
    {
        m_RigidBody = GetComponent<Rigidbody>();
        m_MeshRenderer = GetComponent<MeshRenderer>();
        MovementEnabled = true;
    }

    /// <summary>
    /// Handles disabling the MeshRenderer when the client despawns a NetworkObject
    /// </summary>
    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            if (m_MeshRenderer == null)
            {
                m_MeshRenderer = GetComponent<MeshRenderer>();
            }

            if (m_MeshRenderer != null)
            {
                m_MeshRenderer.enabled = false;
            }

            var objectLabel = GetComponentInChildren<NetworkObjectLabel>();
            if (objectLabel != null)
            {
                objectLabel.enabled = false;
            }
        }

        base.OnNetworkDespawn();
    }

    /// <summary>
    /// Handles setting a delay before the newly spawned object is visible
    /// Note: this might get removed once the snapshot system is synchronizing
    /// NetworkObjects' spawn and despawn.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            if (m_MeshRenderer == null)
            {
                m_MeshRenderer = GetComponent<MeshRenderer>();
            }
            m_MeshRenderer.enabled = false;
            var objectLabel = GetComponentInChildren<NetworkObjectLabel>();
            if (objectLabel != null)
            {
                objectLabel.enabled = false;
            }

            m_VisibilitySpawn = Time.realtimeSinceStartup + 0.12f;
        }
        base.OnNetworkSpawn();
    }

    /// <summary>
    /// Sets the object's direction and velocity
    /// </summary>
    /// <param name="direction">vector3 direction</param>
    /// <param name="velocity">float velocity</param>
    public void SetDirectionAndVelocity(Vector3 direction, float velocity)
    {
        m_Direction = direction;
        m_Direction.Normalize();
        m_Direction.y = 0;
        m_Velocity = velocity;
    }

    /// <summary>
    /// Handles moving the object based on its current direction and velocity
    /// </summary>
    private void FixedUpdate()
    {
        if (NetworkManager != null && NetworkManager.IsListening)
        {
            if (IsOwner && MovementEnabled)
            {
                m_RigidBody.MovePosition(transform.position + m_Direction * (m_Velocity * Time.fixedDeltaTime));

                if (m_MoveRandomly && Random.Range(0.0f, 1.0f) < 0.01f)
                {
                    var dir = Random.insideUnitCircle;
                    m_Direction.x = dir.x;
                    m_Direction.z = dir.y;
                }
            }
            else
            {
                // All of the below is strictly for debugging purposes.  None of these conditions should ever be true.
                if (NetworkObject != null && !NetworkObject.isActiveAndEnabled)
                {
                    Debug.LogWarning($"{nameof(GenericPooledObjectBehaviour)} id {NetworkObject.NetworkObjectId} is not active and enabled but game object is still active!");
                }

                if (NetworkObject != null && !NetworkObject.IsSpawned)
                {
                    Debug.LogWarning($"{nameof(GenericPooledObjectBehaviour)} id {NetworkObject.NetworkObjectId} is not spawned but still active and enabled (forcing set active false)");
                    gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// This update handles server side despawning and enables client side visibility
    /// </summary>
    private void Update()
    {
        if (IsOwner && m_ShouldDespawn && NetworkObject != null)
        {
            m_ShouldDespawn = false;

            NetworkObject.Despawn(HasHandler);
            if (!HasHandler)
            {
                NetworkObject.gameObject.SetActive(false);
                NetworkObject.gameObject.transform.position = Vector3.zero;
            }
        }
        else if (!IsServer)
        {
            // This is here to handle any short term latency between the time
            // an object becomes spawned to the time it takes to update its first
            // position.
            if (m_MeshRenderer != null && !m_MeshRenderer.enabled)
            {
                if (m_VisibilitySpawn < Time.realtimeSinceStartup)
                {
                    m_MeshRenderer.enabled = true;
                    var objectLabel = GetComponentInChildren<NetworkObjectLabel>();
                    if (objectLabel != null)
                    {
                        objectLabel.enabled = true;
                    }
                }
            }
        }

    }

    /// <summary>
    /// Host and Server Only:
    /// When triggered this will start the despawn process
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerEnter(Collider other)
    {
        if (IsOwner && gameObject.activeInHierarchy)
        {
            if (other.CompareTag("GenericObject") || other.CompareTag("Floor"))
            {
                return;
            }
            else
            {
                m_ShouldDespawn = true;
            }
        }
    }
}
