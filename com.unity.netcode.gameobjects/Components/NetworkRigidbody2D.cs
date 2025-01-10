#if COM_UNITY_MODULES_PHYSICS2D
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// NetworkRigidbody allows for the use of <see cref="Rigidbody2D"/> on network objects. By controlling the kinematic
    /// mode of the rigidbody and disabling it on all peers but the authoritative one.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(NetworkTransform))]
    [AddComponentMenu("Netcode/Network Rigidbody 2D")]
    public class NetworkRigidbody2D : NetworkBehaviour
    {
        /// <summary>
        /// Determines if we are server (true) or owner (false) authoritative
        /// </summary>
        private bool m_IsServerAuthoritative;

        private Rigidbody2D m_Rigidbody;
        private NetworkTransform m_NetworkTransform;

        private RigidbodyInterpolation2D m_OriginalInterpolation;

        // Used to cache the authority state of this rigidbody during the last frame
        private bool m_IsAuthority;

        private void Awake()
        {
            m_NetworkTransform = GetComponent<NetworkTransform>();
            m_IsServerAuthoritative = m_NetworkTransform.IsServerAuthoritative();

            SetupRigidBody();
        }

        /// <summary>
        /// If the current <see cref="NetworkTransform"/> has authority,
        /// then use the <see cref="Rigidbody2D"/> interpolation strategy,
        /// if the <see cref="NetworkTransform"/> is handling interpolation,
        /// set interpolation to none on the <see cref="Rigidbody2D"/>
        /// <br/>
        /// Turn off physics for the rigid body until spawned, otherwise
        /// clients can run fixed update before the first
        /// full <see cref="NetworkTransform"/> update
        /// </summary>
        private void SetupRigidBody()
        {
            m_Rigidbody = GetComponent<Rigidbody2D>();
            m_OriginalInterpolation = m_Rigidbody.interpolation;

            m_Rigidbody.interpolation = m_IsAuthority ? m_OriginalInterpolation : (m_NetworkTransform.Interpolate ? RigidbodyInterpolation2D.None : m_OriginalInterpolation);
            // Turn off physics for the rigid body until spawned, otherwise
            // clients can run fixed update before the first full
            // NetworkTransform update
            SetIsKinematic(true);
        }

        private void SetIsKinematic(bool isKinematic)
        {
#if UNITY_2022_3_OR_NEWER
            m_Rigidbody.bodyType = isKinematic ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
#else
            m_Rigidbody.isKinematic = isKinematic;
#endif
        }

        /// <summary>
        /// For owner authoritative (i.e. ClientNetworkTransform)
        /// we adjust our authority when we gain ownership
        /// </summary>
        public override void OnGainedOwnership()
        {
            UpdateOwnershipAuthority();
        }

        /// <summary>
        /// For owner authoritative(i.e. ClientNetworkTransform)
        /// we adjust our authority when we have lost ownership
        /// </summary>
        public override void OnLostOwnership()
        {
            UpdateOwnershipAuthority();
        }

        /// <summary>
        /// Sets the authority differently depending upon
        /// whether it is server or owner authoritative
        /// </summary>
        private void UpdateOwnershipAuthority()
        {
            if (m_IsServerAuthoritative)
            {
                m_IsAuthority = NetworkManager.IsServer;
            }
            else
            {
                m_IsAuthority = IsOwner;
            }

            // If you have authority then you are not kinematic
            SetIsKinematic(!m_IsAuthority);

            // Set interpolation of the Rigidbody2D based on authority
            // With authority: let local transform handle interpolation
            // Without authority: let the NetworkTransform handle interpolation
            m_Rigidbody.interpolation = m_IsAuthority ? m_OriginalInterpolation : RigidbodyInterpolation2D.None;
        }

        /// <inheritdoc />
        public override void OnNetworkSpawn()
        {
            UpdateOwnershipAuthority();
        }

        /// <inheritdoc />
        public override void OnNetworkDespawn()
        {
            UpdateOwnershipAuthority();
        }
    }
}
#endif // COM_UNITY_MODULES_PHYSICS2D
