using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace TestProject.ManualTests
{
    /// <summary>
    /// A general object that can be used for testing purposes
    /// </summary>
    public class GenericNetworkObjectBehaviour : NetworkBehaviour
    {
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

        [SerializeField]
        [Tooltip("This will make the spawned objects move around randomly.  !Caution! You can generate a lot of objects this way!")]
        private bool m_MoveRandomly = true;

        [SerializeField]
        private bool m_Rotate = false;

        [SerializeField]
        private Vector3 m_RotationAmount = new Vector3(0.0f, 1.0f, 0.0f);

        [Header("Manual Testing")]

        [SerializeField]
        [Tooltip("When enabled this will make connected clients attempt to change the transform locally which should generate a" +
            " console log warning message on the client side.")]
        private bool m_TestClientSideNotifcation = false;

        private Rigidbody m_RigidBody;
        private MeshRenderer m_MeshRenderer;
        private NetworkTransform m_NetworkTransform;
        private Vector3 m_Direction;
        private float m_Velocity;

        private void Start()
        {
            m_RigidBody = GetComponent<Rigidbody>();
            m_MeshRenderer = GetComponent<MeshRenderer>();
            m_NetworkTransform = GetComponent<NetworkTransform>();
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
            // Don't do anything until spawned
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                m_RigidBody.MovePosition(transform.position + m_Direction * (m_Velocity * Time.fixedDeltaTime));

                if (m_Rotate && m_NetworkTransform != null && (m_NetworkTransform.SyncRotAngleX || m_NetworkTransform.SyncRotAngleY || m_NetworkTransform.SyncRotAngleZ))
                {
                    transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + m_RotationAmount);
                }

                if (m_MoveRandomly && Random.Range(0.0f, 1.0f) < 0.01f)
                {
                    var dir = Random.insideUnitCircle;
                    m_Direction.x = dir.x;
                    m_Direction.z = dir.y;
                }
            }
            else
            {
                if (m_TestClientSideNotifcation)
                {
                    // When LogLevel is Developer this should generate a warning message on the client side
                    m_RigidBody.MovePosition(transform.position + Vector3.one * (2.0f * Time.fixedDeltaTime));
                }

                if (NetworkObject != null && !NetworkObject.isActiveAndEnabled)
                {
                    Debug.LogWarning($"{nameof(GenericNetworkObjectBehaviour)} id {NetworkObject.NetworkObjectId} is not active and enabled but game object is still active!");
                }
            }
        }

        private void Update()
        {
            if (IsOwner && m_ShouldDespawn && NetworkObject != null)
            {
                m_ShouldDespawn = false;
                if (IsServer)
                {
                    NetworkObject.Despawn();
                }
                else
                {
                    DespawnServerRpc();
                }
            }
        }

        [ServerRpc]
        private void DespawnServerRpc(ServerRpcParams serverRpcParams = default)
        {
            if (OwnerClientId == serverRpcParams.Receive.SenderClientId)
            {
                NetworkObject.Despawn();
            }
        }

        public override void OnNetworkDespawn()
        {
            if (m_RigidBody != null)
            {
                m_RigidBody.Sleep();
                m_RigidBody.transform.position = Vector3.zero;
            }
            m_Direction = Vector3.zero;

            base.OnNetworkDespawn();
        }

        [HideInInspector]
        public bool HasHandler;

        private bool m_ShouldDespawn;

        private void OnTriggerEnter(Collider other)
        {
            if (IsSpawned && IsOwner && !m_ShouldDespawn)
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
