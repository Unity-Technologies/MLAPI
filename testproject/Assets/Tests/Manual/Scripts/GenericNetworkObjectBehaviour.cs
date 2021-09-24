using Unity.Netcode;
using UnityEngine;

namespace TestProject.ManualTests
{
    /// <summary>
    /// A general object that can be used for testing purposes
    /// </summary>
    public class GenericNetworkObjectBehaviour : NetworkBehaviour
    {
        [SerializeField]
        [Tooltip("This will make the spawned objects move around randomly.  !Caution! You can generate a lot of objects this way!")]
        private bool m_MoveRandomly = true;

        private Rigidbody m_RigidBody;
        private MeshRenderer m_MeshRenderer;
        private Vector3 m_Direction;
        private float m_Velocity;

        private void Start()
        {
            m_RigidBody = GetComponent<Rigidbody>();
            m_MeshRenderer = GetComponent<MeshRenderer>();
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
            }
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Makes mesh renderer visible again
        /// </summary>
        public void Reset()
        {
            if (m_MeshRenderer == null)
            {
                m_MeshRenderer = GetComponent<MeshRenderer>();
            }
            m_MeshRenderer.enabled = true;
        }

        private float m_VisibilitySpawn;
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
                m_VisibilitySpawn = Time.realtimeSinceStartup + 0.12f;
                if (NetworkObject.NetworkObjectId == 0)
                {
                    Debug.Log("Spawning NetworkObjectId 0!");
                }
            }
            base.OnNetworkSpawn();
        }

        public void ShouldMoveRandomly(bool shouldMoveRandomly)
        {
            m_MoveRandomly = shouldMoveRandomly;
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
            if (IsSpawned && NetworkManager != null && NetworkManager.IsListening)
            {
                if (IsOwner)
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
                    if (NetworkObject != null && !NetworkObject.isActiveAndEnabled)
                    {
                        Debug.LogWarning($"{nameof(GenericNetworkObjectBehaviour)} id {NetworkObject.NetworkObjectId} is not active and enabled but game object is still active!");
                    }
                }
            }
        }

        /// <summary>
        /// Tells us that we are registered with a NetworkPefab pool
        /// This is primarily for late joining clients and object synchronization.
        /// </summary>
        public bool IsRegisteredPoolObject;

        /// <summary>
        /// This tells us that the NetworkObject has been removed from a pool
        /// This is primarily to handle NetworkPrefab pool that was loaded in an additive scene and the
        /// additive scene was unloaded but the NetworkObject persisted (i.e. was spawned in a different scene)
        /// </summary>
        public bool IsRemovedFromPool;

        private void Update()
        {
            if (IsSpawned)
            {
                if (IsOwner && m_ShouldDespawn && NetworkObject != null)
                {
                    m_ShouldDespawn = false;
                    if (NetworkObject.NetworkManager != null)
                    {
                        NetworkObject.Despawn(true);
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
                        }
                    }
                }
            }
            else if (!IsSpawned && IsRegisteredPoolObject)
            {
                if (m_MeshRenderer != null && m_MeshRenderer.enabled)
                {
                    m_MeshRenderer.enabled = false;
                    gameObject.SetActive(false);
                }
            }

        }

        [HideInInspector]
        public bool HasHandler;

        private bool m_ShouldDespawn;

        private void OnTriggerEnter(Collider other)
        {
            if (IsOwner && !m_ShouldDespawn)
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
}
